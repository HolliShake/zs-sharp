using System.Runtime.InteropServices;
using System.Text;

namespace zscript;

public class Vm
{
    public readonly ZsValue Error;
    public readonly ZsValue Future;
    public readonly ZsValue Null;
    public readonly ZsValue Object;
    public readonly Queue<ZsValue> PendingTasks = new();
    private readonly State State;
    public readonly ZsValue TypeError;

    public Vm(State state)
    {
        State = state;
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
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (+)")
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
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (-)")
        };

        // Console.WriteLine($"{a} - {b} = {res}");
        return res;
    }

    private ZsValue DoLoadFunction(Frame callerFrame, int off)
    {
        var codeObject = State.Codes[off];
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
        // Console.WriteLine($"{address} = {val.GetZsType()}");
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

        return Run(newCallFrame);
    }

    private ZsValue DoCallMethod(Frame frame, int arg)
    {
        var zsObject = frame.PeekOperandAt(arg + 1);
        var memberName = frame.PopOperand();

        if (ZsValue.IsInstanceOf(zsObject, ValueType.Future))
            switch (memberName.String())
            {
                case "then":
                    return zscript.Future.FutureThenMethod(this, [zsObject, frame.PopOperand()]);
            }

        throw new Exception($"method not found {zsObject.GetZsType()}.{memberName}");
    }

    private static void RaiseOrHandleException(Frame frame, ZsValue errorValue)
    {
        frame.Suspend();
        var error = ZsValue.GetProperty(errorValue, "message");
        if (error != null) throw new Exception(error.String());
    }

    public ZsValue Run(Frame frame)
    {
        var code = frame.FunctionValue.Code();
        while (frame.Pc < frame.CodeLen && !frame.Suspended)
        {
            var opcode = (OpCode)code.Bytecode[frame.Pc++];

            switch (opcode)
            {
                case OpCode.LOADLOCAL:
                case OpCode.LOADCAPTURE:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    var val = frame.GetEnvVar(off);
                    if (val == null)
                    {
                        RaiseOrHandleException(frame, ZsValue.FromErrorMessage(Error, "referenced before assignment"));
                        break;
                    }

                    frame.PushOperand(val);
                    break;
                }
                case OpCode.LOADCONST:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    frame.PushOperand(State.Constants[off]);
                    break;
                }
                case OpCode.LOADSTRING:
                {
                    var str = ReadString(frame);
                    frame.Forward(str.Length + 1);
                    frame.PushOperand(ZsValue.FromString(str));
                    break;
                }
                case OpCode.LOADNULL:
                {
                    frame.PushOperand(Null);
                    break;
                }
                case OpCode.LOADFUNCTION:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    var function = DoLoadFunction(frame, off);
                    frame.PushOperand(function);
                    break;
                }
                case OpCode.STORENAME:
                case OpCode.STORELOCAL:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    DoStoreName(frame, off);
                    break;
                }
                case OpCode.CALL:
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
                case OpCode.CALLMETHOD:
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
                case OpCode.AWAIT:
                {
                    var zsFuture = frame.PopOperand();
                    if (!ZsValue.IsInstanceOf(zsFuture, ValueType.Future))
                    {
                        frame.PushOperand(zsFuture);
                        break;
                    }

                    var futureInstance = zsFuture.Future();

                    // if (futureInstance.State == FutureState.FULLFILL)
                    // {
                    //     frame.PushOperand(futureInstance.Result!);
                    //     break;
                    // }
                    //
                    // if (futureInstance.State == FutureState.REJECTED)
                    //     // Raise error
                    //     break;

                    frame.Suspend();
                    if (frame.Future == null)
                    {
                        var future = ZsValue.FromFuture(new Future(FutureState.PENDING, frame));
                        frame.SetFutureOrSkip(future);

                        if (futureInstance.State == FutureState.FULLFILL ||
                            futureInstance.State == FutureState.REJECTED)
                        {
                            frame.PushOperand(futureInstance.Result!);
                            PendingTasks.Enqueue(future);
                            return future;
                        }

                        futureInstance.AddListener(future);
                        return future;
                    }

                    if (futureInstance.State == FutureState.FULLFILL ||
                        futureInstance.State == FutureState.REJECTED)
                    {
                        frame.PushOperand(futureInstance.Result!);
                        PendingTasks.Enqueue(frame.Future);
                        return frame.Future;
                    }

                    futureInstance.AddListener(frame.Future);
                    return frame.Future;
                }
                case OpCode.PRINT:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    DoPrint(frame, size);
                    break;
                }
                case OpCode.BINADD:
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
                case OpCode.BINSUB:
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
                case OpCode.POPTOP:
                {
                    frame.PopOperand();
                    break;
                }
                case OpCode.RETURN:
                {
                    if (frame.Asynchronous || frame.Future != null)
                    {
                        // Console.WriteLine(frame.Future != null);
                        var zsFuture = frame.Future != null
                            ? frame.Future
                            : frame.Future = ZsValue.FromFuture(new Future(FutureState.FULLFILL, frame, null!));

                        zsFuture.Future()
                            .FullFill(frame.PopOperand(), PendingTasks);

                        return zsFuture!;
                    }

                    return frame.PopOperand();
                }
                default:
                {
                    throw new NotImplementedException($"OpCode {opcode} not implemented");
                }
            }
        }

        return ZsValue.FromInt(0);
    }

    public void MainLoop(ZsValue globalCodeObject)
    {
        Run(new Frame(null, globalCodeObject, false, false));

        while (PendingTasks.Count > 0)
        {
            // Console.WriteLine("Continuing...");
            var nextTask = PendingTasks.Dequeue();
            var futureInstance = nextTask.Future();
            futureInstance.SuspendedFrame.Wake();
            Run(futureInstance.SuspendedFrame);
        }
    }
}