using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace zscript;

public class Vm
{
    private readonly State _state;
    public readonly Queue<ZsValue> DeferredTasks = new();
    public readonly ZsValue Error;
    public readonly ZsValue False;
    public readonly ZsValue Future;
    public readonly ZsValue Null;
    public readonly ZsValue Object;
    public readonly Queue<ZsValue> PendingTasks = new();
    public readonly ZsValue True;
    public readonly ZsValue TypeError;
    public readonly ZsValue ZeroDivideError;
    private ZsValue? _currentError;
    private Frame? _currentFrame;

    public Vm(State state)
    {
        _state = state;
        Object = ZsValue.CreateZsClass(null, "Object");
        Error = ZsValue.CreateZsClass(Object, "Error");
        TypeError = ZsValue.CreateZsClass(Error, "TypeError");
        ZeroDivideError = ZsValue.CreateZsClass(Error, "ZeroDivideError");
        Future = ZsValue.CreateZsClass(Object, "Future");
        Null = ZsValue.CreateNull();
        True = ZsValue.FromBool(true);
        False = ZsValue.FromBool(false);
        _currentFrame = null;
        _currentError = null;
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

        while (endMatch < code.Bytecode.Count && code.Bytecode[endMatch] != 0) endMatch++;

        var stringLength = endMatch - frame.Pc;

        if (stringLength <= 0) return string.Empty;

        ReadOnlySpan<byte> stringBytes = CollectionsMarshal.AsSpan(code.Bytecode)
            .Slice(frame.Pc, stringLength);

        var result = Encoding.UTF8.GetString(stringBytes);

        return result;
    }

    private ZsValue DoMul(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Number() *
                b.Number()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (*)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoDiv(ZsValue a, ZsValue b)
    {
        if (b is { Type: ValueType.Int } or { Type: ValueType.Number } && b.Number() == 0)
            return ZsValue.FromErrorMessage(ZeroDivideError, "zero division error", BuildTracebackFromFrame());

        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Number() /
                b.Number()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (*)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoMod(ZsValue a, ZsValue b)
    {
        if (b is { Type: ValueType.Int } or { Type: ValueType.Number } && b.Number() == 0)
            return ZsValue.FromErrorMessage(ZeroDivideError, "zero division error", BuildTracebackFromFrame());

        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Number() %
                b.Number()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (%)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoAdd(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Number() +
                b.Number()),
            (ValueType.String, ValueType.String) => ZsValue.FromString(a.String() + b.String()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (+)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoSub(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Number() -
                b.Number()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (-)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoLshift(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Int() <<
                b.Int()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (<<)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoRshift(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(a.Int() >>
                b.Int()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (>>)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoLt(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number() < b.Number()
                ? True
                : False,
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (<)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoLe(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number() <= b.Number()
                ? True
                : False,
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (<=)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoGt(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number() > b.Number()
                ? True
                : False,
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (>)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoGe(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number() >= b.Number()
                ? True
                : False,
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (>=)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoEq(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number() == b.Number()
                ? True
                : False,
            _ => a.Ref == b.Ref || a == b ? True : False
        };

        return res;
    }

    private ZsValue DoNe(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number() != b.Number()
                ? True
                : False,
            _ => a.Ref != b.Ref || a != b ? True : False
        };

        return res;
    }

    private ZsValue DoAnd(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(
                a.Int() & b.Int()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (&)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoOr(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(
                a.Int() | b.Int()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (|)",
                BuildTracebackFromFrame())
        };

        return res;
    }

    private ZsValue DoXor(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => ZsValue.FromNumber(
                a.Int() ^ b.Int()),
            _ => ZsValue.FromErrorMessage(TypeError,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (^)",
                BuildTracebackFromFrame())
        };

        return res;
    }


    private ZsValue DoMakeArray(Frame frame, int size)
    {
        var array = new List<ZsValue>(size);
        for (var i = 0; i < size; i++) array.Add(frame.PopOperand());
        array.Reverse();
        return ZsValue.FromArray(array);
    }

    private ZsValue DoMakeObject(Frame frame, int size)
    {
        var dict = new Dictionary<string, ZsValue>();
        for (var i = 0; i < size; i++)
        {
            var val = frame.PopOperand();
            var key = frame.PopOperand().ToString();
            dict[key] = val;
        }

        return ZsValue.CreateZsObjectLiteral(Object, dict.Reverse().ToDictionary(x => x.Key, x => x.Value));
    }

    private ZsValue DoLoadFunction(Frame callerFrame, int off)
    {
        var codeObjectTemplate = _state.Codes[off];
        var codeObject = codeObjectTemplate.Clone();
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
        var arguments = new ZsValue[arg];
        for (var i = 0; i < arg; i++) arguments[i] = frame.PopOperand();

        if (ZsValue.IsInstanceOf(callable, ValueType.NativeFunction))
        {
            var nativeFunction = callable.NativeFunction();
            return nativeFunction(this, arguments);
        }
        
        var callableCode = callable.Code();

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

        // Pop this
        frame.PopOperand();

        var memberNameString = memberName.String();
        if (ZsValue.IsInstanceOf(zsObject, ValueType.Future) && zscript.Future.HasMethod(memberNameString))
            return zscript.Future.GetMethod(memberNameString)(this, arguments);
        
        var callableProperty = ZsValue.GetProperty(zsObject, memberNameString);
        if (callableProperty == null) return ZsValue.FromErrorMessage(Error, $"object has no attribute {memberNameString}", BuildTracebackFromFrame());
        
        if (ZsValue.IsInstanceOf(callableProperty, ValueType.NativeFunction))
        {
            var nativeFunction = callableProperty.NativeFunction();
            return nativeFunction(this, arguments);
        }
        
        var callablePropertyCode = callableProperty.Code();

        var newCallFrame = new Frame(frame, callableProperty, false, callablePropertyCode.IsAsync);
        for (var i = 1; i <= arg; i++) newCallFrame.PushOperand(arguments[i]);
        
        // Set this
        newCallFrame.SetEnvVar(0, zsObject);
        
        callablePropertyCode.MergeCaptureToEnvironment(newCallFrame);

        return callablePropertyCode.ArgCount != arg
            ? ZsValue.FromErrorMessage(Error, $"arg mismatch {callablePropertyCode.ArgCount} != {arg}",
                BuildTracebackFromFrame())
            : Run(newCallFrame);
    }

    private void RaiseOrHandleException(Frame thrownByFrame, ZsValue errorValue)
    {
        var current = thrownByFrame;

        while (current != null)
        {
            // 1. Synchronous Catch: Does this frame have a try/catch block?
            if (current.HasTryHandler())
            {
                current.PushOperand(errorValue);

                var tryBlock = current.PeekTryTable();
                current.JumpTo(tryBlock.ToPc);

                current.Wake();

                return;
            }

            // 2. Async Boundary: If no try/catch, does this escape an async function?
            if (current.Asynchronous || current.Future != null)
            {
                current.Suspend();

                var zsFuture = current.Future ?? ZsValue.FromFuture(new Future(FutureState.Rejected, current));
                current.SetFutureOrSkip(zsFuture);

                zsFuture.Future().Reject(errorValue, null);

                PendingTasks.Enqueue(zsFuture);

                _currentFrame = current.CallerFrame;
                return;
            }

            current.Terminate();
            current = current.CallerFrame;
        }

        _currentError = errorValue;
        Console.WriteLine($"[VM Fatal] Uncaught Exception: {errorValue}");
        PendingTasks.Clear();
    }

    private static OpCodeDebug GetLine(List<OpCodeDebug> debugLines, long pc)
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

        if (frame.PendingError != null)
        {
            var err = frame.PendingError;
            frame.PendingError = null;

            RaiseOrHandleException(frame, err);

            if (frame.Suspended) return frame.Future!;
        }

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
                case OpCode.MakeArray:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    var arrayValue = DoMakeArray(frame, size);
                    frame.PushOperand(arrayValue);
                    break;
                }
                case OpCode.MakeObject:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    var objectValue = DoMakeObject(frame, size);
                    frame.PushOperand(objectValue);
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

                    if (!frame.Suspended) frame.PushOperand(ret);
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

                    if (!frame.Suspended) frame.PushOperand(ret);
                    break;
                }
                case OpCode.DupTop:
                {
                    frame.PushOperand(frame.PeekOperand());
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
                    }

                    futureInstance.AddListener(frame.Future!);

                    return frame.Future!;
                }
                case OpCode.Print:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    DoPrint(frame, size);
                    break;
                }
                case OpCode.BinMul:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoMul(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinDiv:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoDiv(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinMod:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoMod(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
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
                case OpCode.BinLshift:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoLshift(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinRshift:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoRshift(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.CmpLt:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoLt(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.CmpLe:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoLe(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.CmpGt:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoGt(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.CmpGe:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoGe(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.CmpEq:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoEq(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.CmpNe:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoNe(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinAnd:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoAnd(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinOr:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoOr(b, a);
                    if (ZsValue.IsInstanceOf(c, "Error"))
                    {
                        RaiseOrHandleException(frame, c);
                        break;
                    }

                    frame.PushOperand(c);
                    break;
                }
                case OpCode.BinXor:
                {
                    var a = frame.PopOperand();
                    var b = frame.PopOperand();
                    var c = DoXor(b, a);
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
                    frame.Forward(4);
                    var fromLine = GetLine(code.DebugLines, frame.Pc - 1);
                    var toLine = GetLine(code.DebugLines, frame.Pc - 1);
                    frame.PushTryTable(new TryBlock(frame.Pc - 1, jmp, fromLine.Line, toLine.Line));
                    break;
                }
                case OpCode.PopTry:
                {
                    frame.PopTryTable();
                    break;
                }
                case OpCode.JumpIfFalseOrPop:
                {
                    var jmp = ReadInt(frame);
                    frame.Forward(4);
                    var con = frame.PeekOperand();
                    if (!con.Bool())
                        frame.JumpTo(jmp);
                    else
                        frame.PopOperand();
                    break;
                }
                case OpCode.JumpIfTrueOrPop:
                {
                    var jmp = ReadInt(frame);
                    frame.Forward(4);
                    var con = frame.PeekOperand();
                    if (con.Bool())
                        frame.JumpTo(jmp);
                    else
                        frame.PopOperand();
                    break;
                }
                case OpCode.PopJumpIfFalse:
                {
                    var jmp = ReadInt(frame);
                    frame.Forward(4);
                    if (!frame.PopOperand().Bool()) frame.JumpTo(jmp);
                    break;
                }
                case OpCode.PopJumpIfTrue:
                {
                    var jmp = ReadInt(frame);
                    frame.Forward(4);
                    if (frame.PopOperand().Bool()) frame.JumpTo(jmp);
                    break;
                }
                case OpCode.Jump:
                {
                    var jmp = ReadInt(frame);
                    frame.JumpTo(jmp);
                    break;
                }
                case OpCode.Return:
                {
                    var count = frame.GetOperandCount();
                    Debug.Assert(count == 1, $"{code.Name} -> frame.GetOperandCount()({count}) != 1");

                    _currentFrame = _currentFrame.CallerFrame;

                    if (frame.Asynchronous || frame.Future != null)
                    {
                        var zsFuture = frame.Future != null!
                            ? frame.Future
                            : ZsValue.FromFuture(new Future(FutureState.Fulfill, frame, null!));

                        frame.SetFutureOrSkip(zsFuture);

                        zsFuture.Future()
                            .FullFill(frame.PopOperand(), frame.IsCallback ? null : PendingTasks);

                        if (frame.IsCallback) DeferredTasks.Enqueue(zsFuture);

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

        // 1 := fail
        return frame.Future ?? ZsValue.FromInt(1);
    }

    public void MainLoop(ZsValue globalCodeObject)
    {
        Run(new Frame(null, globalCodeObject, false, false));

        while (PendingTasks.Count > 0 || DeferredTasks.Count > 0)
        {
            if (PendingTasks.Count == 0)
                while (DeferredTasks.Count > 0)
                    PendingTasks.Enqueue(DeferredTasks.Dequeue());

            var nextTask = PendingTasks.Dequeue();
            var futureInstance = nextTask.Future();
            switch (futureInstance.State)
            {
                case FutureState.Fulfill:
                {
                    futureInstance.FullFill(futureInstance.Result!, PendingTasks);
                    break;
                }
                case FutureState.Rejected:
                {
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