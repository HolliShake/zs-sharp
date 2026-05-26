using System.Runtime.InteropServices;
using System.Text;

namespace zscript;

public class Vm
{
    private readonly State _state;
    public readonly ZsValue Error;
    public readonly ZsValue Future;
    public readonly ZsValue Null;
    public readonly ZsValue Object;
    public readonly Queue<ZsValue> PendingTasks = new();
    public readonly ZsValue TypeError;
    private Frame? _currentFrame;

    public Vm(State state)
    {
        _state = state;
        Object = ZsValue.CreateZsClass(null, "Object");
        Error = ZsValue.CreateZsClass(Object, "Error");
        TypeError = ZsValue.CreateZsClass(Error, "TypeError");
        Future = ZsValue.CreateZsClass(Object, "Future");
        Null = ZsValue.CreateNull();
    }

    private int ReadInt(Frame frame)
    {
        var code = frame.FunctionValue.Code();
        var byte0 = code.Bytecode[frame.Pc];
        var byte1 = code.Bytecode[frame.Pc + 1];
        var byte2 = code.Bytecode[frame.Pc + 2];
        var byte3 = code.Bytecode[frame.Pc + 3];
        return (byte0 << 24) | (byte1 << 16) | (byte2 << 8) | (byte3 << 0);
    }

    private string ReadString(Frame frame)
    {
        var code = frame.FunctionValue.Code();

        var endMatch = frame.Pc;

        // Scan until we find the null terminator
        while (endMatch < code.Bytecode.Count && code.Bytecode[endMatch] != 0) endMatch++;

        // Calculate the actual number of string bytes relative to Pc
        var stringLength = endMatch - frame.Pc;

        if (stringLength <= 0) return string.Empty;

        // Slice exactly from the current Pc for 'stringLength' bytes
        ReadOnlySpan<byte> stringBytes = CollectionsMarshal.AsSpan(code.Bytecode)
            .Slice(frame.Pc, stringLength);

        var result = Encoding.UTF8.GetString(stringBytes);

        return result;
    }

    private ZsValue DoAdd(ZsValue a, ZsValue b)
    {
        var res = (a.Value, b.Value) switch
        {
            (int, int) => ZsValue.FromInt(a.Int() + b.Int()),
            (double or int, double or int) => ZsValue.FromNumber(a.Number() + b.Number()),
            (string, string) => ZsValue.FromString(a.String() + b.String()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (+)",
                BuildTracebackFromFrame())
        };

        // Console.WriteLine($"{a} + {b} = {res}");
        return res;
    }

    private ZsValue DoSub(ZsValue a, ZsValue b)
    {
        var res = (a.Value, b.Value) switch
        {
            (int, int) => ZsValue.FromInt(a.Int() - b.Int()),
            (double or int, double or int) => ZsValue.FromNumber(a.Number() - b.Number()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (-)",
                BuildTracebackFromFrame())
        };

        // Console.WriteLine($"{a} - {b} = {res}");
        return res;
    }

    private ZsValue DoLoadFunction(Frame callerFrame, int off)
    {
        var codeObject = _state.Codes[off];
        var function = ZsValue.FromCodeToFunction(codeObject);

        foreach (var (depth, address, destination) in codeObject.Captures)
        {
            var currentFrame = callerFrame;
            var currentDepth = 1;

            while (currentFrame != null && currentDepth < depth)
            {
                currentDepth++;
                currentFrame = currentFrame.CallerFrame;
            }

            if (currentFrame == null)
                throw new InvalidOperationException($"Closure capture failed: Frame at depth {depth} does not exist.");

            var capturedVariable = currentFrame.Environment[address];
            codeObject.CapturedCells[destination] = capturedVariable;
        }

        return function;
    }

    private static void DoPrint(Frame frame, int size)
    {
        var args = new object[size];

        for (var i = size - 1; i >= 0; i--) args[i] = frame.PopOperand();

        Console.WriteLine(string.Join(" ", args));
    }

    private static void DoStoreName(Frame frame, int address)
    {
        var val = frame.PopOperand();
        frame.SetEnvVar(address, val);
    }

    private ZsValue DoCall(Frame frame, int arg)
    {
        var callable = frame.PopOperand();
        var callableCode = callable.Code();
        var arguments = new ZsValue[arg];
        for (var i = 0; i < arg; i++) arguments[i] = frame.PopOperand();

        var newCallFrame = new Frame(frame, callable, false, callableCode.IsAsync);
        for (var i = 0; i < arg; i++) newCallFrame.PushOperand(arguments[i]);

        callableCode.MergeCaptureToEnvironment(newCallFrame);

        return callableCode.ArgCount != arg
            ? ZsValue.FromErrorMessage(Error, $"arg mismatch {callableCode.ArgCount} != {arg}",
                BuildTracebackFromFrame())
            : Run(newCallFrame);
    }

    private ZsValue DoCallMethod(Frame frame, int arg)
    {
        var zsObject = frame.PeekOperandAt(arg + 1);
        var memberName = frame.PopOperand();

        var arguments = new ZsValue[arg + 1];
        arguments[0] = zsObject;
        for (var i = 1; i < arg + 1; i++) arguments[i] = frame.PopOperand();

        if (ZsValue.IsInstanceOf(zsObject, ValueType.Future))
            switch (memberName.String())
            {
                case "then":
                    return zscript.Future.FutureThenMethod(this, arguments);
                case "catch":
                    return zscript.Future.FutureCatchMethod(this, arguments);
            }

        return ZsValue.FromErrorMessage(Error, $"method not found {zsObject.GetZsType()}.{memberName}",
            BuildTracebackFromFrame());
    }

    private void RaiseOrHandleException(Frame frame, ZsValue errorValue)
    {
        var currentFrame = frame;
        while (currentFrame != null)
        {
            if (currentFrame.HasTryHandler())
            {
                var currentHandler = currentFrame.PopTryTable();
                return;
            }
            currentFrame = currentFrame.CallerFrame;
        }

        if (frame.Asynchronous || frame.Future != null)
        {
            frame.Suspend();
            var fut = frame.Future != null
                ? frame.Future
                : ZsValue.FromFuture(new Future(FutureState.Rejected, frame));

            frame.SetFutureOrSkip(fut);

            fut.Future().Reject(errorValue, PendingTasks);
            frame.PushOperand(errorValue);
            PendingTasks.Enqueue(fut);
            return;
        }

        var error = ZsValue.GetProperty(errorValue, "message");
        if (error != null) throw new Exception(error.String() + "\n" + BuildTracebackFromFrame());
    }

    private OpCodeDebug GetLine(List<OpCodeDebug> debugLines, long pc)
    {
        var low = 0;
        var high = debugLines.Count - 1;
        var bestMatchIndex = -1;

        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var midIndex = debugLines[mid].Index;

            if (midIndex == pc) return debugLines[mid];

            if (midIndex < pc)
            {
                bestMatchIndex = mid;

                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return bestMatchIndex != -1 ? debugLines[bestMatchIndex] : debugLines[0];
    }

    public string BuildTracebackFromFrame()
    {
        var sites = new Queue<string>();

        var currentFrame = _currentFrame;
        while (currentFrame != null)
        {
            var code = currentFrame.FunctionValue.Code();
            var tracebackLine = GetLine(code.DebugLines, currentFrame.Pc);
            var moduleName = _state.ModuleNames[tracebackLine.ModuleId];
            sites.Enqueue($"    at [{moduleName}:{code.Name}:{tracebackLine.Line}]");
            currentFrame = currentFrame.CallerFrame;
        }

        return string.Join(Environment.NewLine, sites);
    }

    public ZsValue Run(Frame frame)
    {
        _currentFrame = frame;
        var code = frame.FunctionValue.Code();
        while (frame.Pc < frame.CodeLen && !frame.Suspended)
        {
            var opcode = (OpCode)code.Bytecode[frame.Pc++];

            switch (opcode)
            {
                case OpCode.LoadLocal:
                case OpCode.LoadCapture:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    var val = frame.GetEnvVar(off);
                    if (val == null)
                    {
                        RaiseOrHandleException(frame,
                            ZsValue.FromErrorMessage(Error, "referenced before assignment", BuildTracebackFromFrame()));
                        break;
                    }

                    frame.PushOperand(val);
                    break;
                }
                case OpCode.LoadConst:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    frame.PushOperand(_state.Constants[off]);
                    break;
                }
                case OpCode.LoadString:
                {
                    var str = ReadString(frame);
                    frame.Forward(str.Length + 1);
                    frame.PushOperand(ZsValue.FromString(str));
                    break;
                }
                case OpCode.LoadNull:
                {
                    frame.PushOperand(Null);
                    break;
                }
                case OpCode.LoadFunction:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    var function = DoLoadFunction(frame, off);
                    frame.PushOperand(function);
                    break;
                }
                case OpCode.StoreName:
                case OpCode.StoreLocal:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    DoStoreName(frame, off);
                    break;
                }
                case OpCode.Call:
                {
                    var arg = ReadInt(frame);
                    frame.Forward(4);
                    var ret = DoCall(frame, arg);
                    if (ZsValue.IsInstanceOf(ret, "Error"))
                    {
                        RaiseOrHandleException(frame, ret);
                        break;
                    }

                    frame.PushOperand(ret);
                    break;
                }
                case OpCode.CallMethod:
                {
                    var arg = ReadInt(frame);
                    frame.Forward(4);
                    var ret = DoCallMethod(frame, arg);
                    if (ZsValue.IsInstanceOf(ret, "Error"))
                    {
                        RaiseOrHandleException(frame, ret);
                        break;
                    }

                    frame.PushOperand(ret);
                    break;
                }
                case OpCode.Await:
                {
                    var zsFuture = frame.PopOperand();
                    if (!ZsValue.IsInstanceOf(zsFuture, ValueType.Future))
                    {
                        frame.PushOperand(zsFuture);
                        break;
                    }

                    var futureInstance = zsFuture.Future();

                    frame.Suspend();
                    if (frame.Future == null)
                    {
                        var future = ZsValue.FromFuture(new Future(FutureState.Pending, frame));
                        frame.SetFutureOrSkip(future);

                        if (futureInstance.State == FutureState.Fulfill ||
                            futureInstance.State == FutureState.Rejected)
                        {
                            frame.PushOperand(futureInstance.Result!);
                            PendingTasks.Enqueue(future);
                            return future;
                        }

                        futureInstance.AddListener(future);
                        return future;
                    }

                    if (futureInstance.State == FutureState.Fulfill ||
                        futureInstance.State == FutureState.Rejected)
                    {
                        frame.PushOperand(futureInstance.Result!);
                        PendingTasks.Enqueue(frame.Future);
                        return frame.Future;
                    }

                    futureInstance.AddListener(frame.Future);
                    return frame.Future;
                }
                case OpCode.Print:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    DoPrint(frame, size);
                    break;
                }
                case OpCode.BinAdd:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoAdd(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinSub:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoSub(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.PopTop:
                {
                    frame.PopOperand();
                    break;
                }
                case OpCode.SetupTry:
                {
                    var jmp = ReadInt(frame);
                    var fromLine = GetLine(code.DebugLines, frame.Pc - 1);
                    var toLine = GetLine(code.DebugLines, frame.Pc - 1);
                    frame.PushTryTable(new TryBlock(frame.Pc-1, jmp, fromLine.Line, toLine.Line));
                    break;
                }
                case OpCode.Return:
                {
                    if (frame.Asynchronous || frame.Future != null)
                    {
                        var zsFuture = frame.Future != null
                            ? frame.Future
                            : ZsValue.FromFuture(new Future(FutureState.Fulfill, frame, null!));

                        frame.SetFutureOrSkip(zsFuture);

                        zsFuture.Future()
                            .FullFill(frame.PopOperand(), PendingTasks);

                        return zsFuture;
                    }

                    return frame.PopOperand();
                }
                default:
                {
                    throw new NotImplementedException($"OpCode {opcode} not implemented");
                }
            }
        }

        return frame.Future ?? ZsValue.FromInt(0);
    }

    public void MainLoop(ZsValue globalCodeObject)
    {
        Run(new Frame(null, globalCodeObject, false, false));

        while (PendingTasks.Count > 0)
        {
            var nextTask = PendingTasks.Dequeue();
            var futureInstance = nextTask.Future();
            switch (futureInstance.State)
            {
                case FutureState.Fulfill:
                {
                    // Wakeup listeners
                    futureInstance.FullFill(futureInstance.Result!, PendingTasks);;
                    break;
                }
                case FutureState.Rejected:
                {
                    // Wakeup listeners
                    futureInstance.Reject(futureInstance.Result!, PendingTasks);
                    break;
                }
                default:
                {
                    futureInstance.SuspendedFrame.Wake();
                    Run(futureInstance.SuspendedFrame);
                    break;
                }
            }
        }
    }
}