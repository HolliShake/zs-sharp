using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace zscript;

public class Vm : IDisposable
{
    private readonly ZsValue _attributeErrorClass;
    private readonly Queue<ZsValue> _deferredTasks = new();
    private readonly ZsValue _indexErrorClass;
    private readonly ZsValue _zeroDivideErrorClass;
    public readonly ZsValue ErrorClass;
    public readonly ZsValue FalseSingleton;
    public readonly ZsValue FutureClass;
    public readonly ZsValue NullSingleton;
    public readonly ZsValue ObjectClass;
    public readonly Queue<ZsValue> PendingTasks = new();
    public readonly State State;
    public readonly ZsValue TrueSingleton;
    public readonly ZsValue TypeErrorClass;
    public Frame? CurrentFrame;

    public Vm(State state)
    {
        State = state;
        ObjectClass = ZsValue.CreateZsClass(null, "Object");
        ErrorClass = ZsValue.CreateZsClass(ObjectClass, "Error");
        _attributeErrorClass = ZsValue.CreateZsClass(ErrorClass, "AttributeError");
        TypeErrorClass = ZsValue.CreateZsClass(ErrorClass, "TypeError");
        _zeroDivideErrorClass = ZsValue.CreateZsClass(ErrorClass, "ZeroDivideError");
        _indexErrorClass = ZsValue.CreateZsClass(ErrorClass, "IndexError");
        FutureClass = ZsValue.CreateZsClass(ObjectClass, "Future");
        NullSingleton = ZsValue.CreateNull();
        TrueSingleton = ZsValue.FromBool(true);
        FalseSingleton = ZsValue.FromBool(false);
        CurrentFrame = null;
    }

    public void Dispose()
    {
        _deferredTasks.Clear();
        PendingTasks.Clear();
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

        while (endMatch < code.Bytecode.Count && code.Bytecode[endMatch] != 0)
            endMatch++;

        var stringLength = endMatch - frame.Pc;

        if (stringLength <= 0)
            return string.Empty;

        ReadOnlySpan<byte> stringBytes = CollectionsMarshal
            .AsSpan(code.Bytecode)
            .Slice(frame.Pc, stringLength);

        var result = Encoding.UTF8.GetString(stringBytes);

        return result;
    }

    private ZsValue DoMul(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Number() * b.Number()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (*)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoDiv(ZsValue a, ZsValue b)
    {
        if (b is { Type: ValueType.Int } or { Type: ValueType.Number } && b.Number() == 0)
            return ZsValue.FromErrorMessage(
                _zeroDivideErrorClass,
                "zero division error",
                BuildTracebackFromFrame()
            );

        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Number() / b.Number()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (*)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoMod(ZsValue a, ZsValue b)
    {
        if (b is { Type: ValueType.Int } or { Type: ValueType.Number } && b.Number() == 0)
            return ZsValue.FromErrorMessage(
                _zeroDivideErrorClass,
                "zero division error",
                BuildTracebackFromFrame()
            );

        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Number() % b.Number()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (%)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoAdd(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Number() + b.Number()),
            (ValueType.String, ValueType.String) => ZsValue.FromString(a.String() + b.String()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (+)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoSub(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Number() - b.Number()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (-)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoLshift(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Int() << b.Int()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (<<)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoRshift(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Int() >> b.Int()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (>>)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoLt(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number()
                < b.Number()
                    ? TrueSingleton
                    : FalseSingleton,
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (<)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoLe(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number()
                <= b.Number()
                    ? TrueSingleton
                    : FalseSingleton,
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (<=)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoGt(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number()
                > b.Number()
                    ? TrueSingleton
                    : FalseSingleton,
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (>)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoGe(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number()
                >= b.Number()
                    ? TrueSingleton
                    : FalseSingleton,
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (>=)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoEq(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number()
                == b.Number()
                    ? TrueSingleton
                    : FalseSingleton,
            _ => a.Ref == b.Ref || a == b ? TrueSingleton : FalseSingleton
        };

        return res;
    }

    private ZsValue DoNe(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) => a.Number()
                != b.Number()
                    ? TrueSingleton
                    : FalseSingleton,
            _ => a.Ref != b.Ref || a != b ? TrueSingleton : FalseSingleton
        };

        return res;
    }

    private ZsValue DoAnd(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Int() & b.Int()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (&)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoOr(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Int() | b.Int()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (|)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoXor(ZsValue a, ZsValue b)
    {
        var res = (a.Type, b.Type) switch
        {
            (ValueType.Number or ValueType.Int, ValueType.Number or ValueType.Int) =>
                ZsValue.FromNumber(a.Int() ^ b.Int()),
            _ => ZsValue.FromErrorMessage(
                TypeErrorClass,
                $"invalid operand types {a.GetZsType()} and {b.GetZsType()} for operator (^)",
                BuildTracebackFromFrame()
            )
        };

        return res;
    }

    private ZsValue DoMakeArray(Frame frame, int size)
    {
        var array = new List<ZsValue>(size);
        for (var i = 0; i < size; i++)
            array.Add(frame.PopOperand());
        array.Reverse();
        return ZsValue.FromArray(array);
    }

    private void DoArrayUnpack(Frame frame, int size)
    {
        var array = frame.PopOperand();
        if (!ZsValue.IsInstanceOf(array, ValueType.Array))
        {
            RaiseOrHandleException(
                frame,
                ZsValue.FromErrorMessage(
                    TypeErrorClass,
                    "cannot unpack non-iterable",
                    BuildTracebackFromFrame()
                )
            );
            return;
        }

        var sharpList = array.Array();
        if (sharpList.Count < size)
        {
            RaiseOrHandleException(
                frame,
                ZsValue.FromErrorMessage(
                    TypeErrorClass,
                    "not enough values to unpack",
                    BuildTracebackFromFrame()
                )
            );
            return;
        }

        for (var i = 0; i < size; i++)
            frame.PushOperand(sharpList[i]);
    }

    private ZsValue DoMakeObject(Frame frame, int size)
    {
        var dict = new Dictionary<string, ZsValue>();
        for (var i = 0; i < size; i++)
        {
            var key = frame.PopOperand().ToString();
            var val = frame.PopOperand();
            dict[key] = val;
        }

        return ZsValue.CreateZsObjectLiteral(
            ObjectClass,
            dict.Reverse().ToDictionary(x => x.Key, x => x.Value)
        );
    }

    private ZsValue DoLoadFunction(Frame callerFrame, int off)
    {
        var codeObjectTemplate = State.Codes[off];
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
                throw new InvalidOperationException(
                    $"Closure capture failed: Frame at depth {depth} does not exist."
                );

            var capturedVariable = currentFrame.Environment[address];
            capturedVariable.IncRef();
            codeObject.CapturedCells[destination] = capturedVariable;
        }

        return function;
    }

    private static void DoPrint(Frame frame, int size)
    {
        var args = new object[size];

        for (var i = size - 1; i >= 0; i--)
            args[i] = frame.PopOperand();

        Console.WriteLine(string.Join(" ", args));
    }

    private static void DoStoreName(Frame frame, int address)
    {
        var val = frame.PopOperand();
        frame.SetEnvVar(address, val);
    }

    private ZsValue DoGetAttr(Frame frame, string attr, bool pop)
    {
        var zsObject = frame.PopOperand();
        var attribute = ZsValue.GetProperty(zsObject, attr);
        if (attribute == null && pop)
            frame.PopOperand();
        return attribute
               ?? ZsValue.FromErrorMessage(
                   _attributeErrorClass,
                   $"object has no attribute {attr}",
                   BuildTracebackFromFrame()
               );
    }

    private ZsValue DoGetIndex(Frame frame)
    {
        var index = frame.PopOperand();
        var zsObject = frame.PopOperand();

        if (ZsValue.IsInstanceOf(zsObject, ValueType.Array) && (ZsValue.IsInstanceOf(index, ValueType.Int) ||
                                                                ZsValue.IsInstanceOf(index, ValueType.Number)))
        {
            var indexValue = index.Int();
            var arr = zsObject.Array();
            var len = arr.Count;
            return indexValue >= 0 && indexValue < len
                ? arr[indexValue]
                : ZsValue.FromErrorMessage(_indexErrorClass, "index out of bounds", BuildTracebackFromFrame());
        }

        if (ZsValue.IsInstanceOf(zsObject, "Object"))
        {
            var attr = zsObject.String();
            var attribute = ZsValue.GetProperty(zsObject, attr);
            return attribute
                   ?? ZsValue.FromErrorMessage(
                       _attributeErrorClass,
                       $"value has no attribute {attr}",
                       BuildTracebackFromFrame()
                   );
        }

        return ZsValue.FromErrorMessage(
            _attributeErrorClass,
            "value cannot be indexed",
            BuildTracebackFromFrame()
        );
    }

    private ZsValue DoCall(Frame frame, int arg)
    {
        var callable = frame.PopOperand();
        var arguments = new ZsValue[arg];
        for (var i = 0; i < arg; i++)
            arguments[i] = frame.PopOperand();

        if (ZsValue.IsInstanceOf(callable, ValueType.NativeFunction))
        {
            var nativeFunction = callable.NativeFunction();
            var reversed = arguments.Reverse().ToArray();
            return nativeFunction(this, reversed);
        }

        var callableCode = callable.Code();

        var newCallFrame = new Frame(frame, callable, false, callableCode.IsAsync);
        for (var i = 0; i < arg; i++)
            newCallFrame.PushOperand(arguments[i]);

        callableCode.MergeCaptureToEnvironment(newCallFrame);

        return callableCode.ArgCount != arg
            ? ZsValue.FromErrorMessage(
                ErrorClass,
                $"{callableCode.Name}: arg mismatch {callableCode.ArgCount} != {arg}",
                BuildTracebackFromFrame()
            )
            : Run(newCallFrame);
    }

    private ZsValue DoCallMethod(Frame frame, int arg)
    {
        var zsObject = frame.PeekOperandAt(arg + 1);
        var memberName = frame.PopOperand();

        var arguments = new ZsValue[arg + 1];
        arguments[0] = zsObject;
        for (var i = 1; i < arg + 1; i++)
            arguments[i] = frame.PopOperand();

        // Pop this
        frame.PopOperand();

        var memberNameString = memberName.ToString();

        if (
            ZsValue.IsInstanceOf(zsObject, ValueType.Future)
            && Future.HasMethod(memberNameString)
        )
            return Future.GetMethod(memberNameString)(this, arguments);
        if (
            ZsValue.IsInstanceOf(zsObject, ValueType.Array)
            && Array.HasMethod(memberNameString)
        )
            return Array.GetMethod(memberNameString)(this, arguments);

        ZsValue? callableProperty = null;

        if (ZsValue.IsInstanceOf(zsObject, ValueType.Array))
        {
            var indexValue = memberName.Int();
            var arr = zsObject.Array();
            var len = arr.Count;
            callableProperty ??= indexValue >= 0 && indexValue < len
                ? arr[indexValue]
                : callableProperty;
        }
        else if (ZsValue.IsInstanceOf(zsObject, "Object"))
        {
            callableProperty = ZsValue.GetProperty(zsObject, memberNameString);
        }

        if (callableProperty == null)
            return ZsValue.FromErrorMessage(
                ErrorClass,
                $"object has no attribute {memberNameString}",
                BuildTracebackFromFrame()
            );

        if (ZsValue.IsInstanceOf(callableProperty, ValueType.NativeFunction))
        {
            var nativeFunction = callableProperty.NativeFunction();
            var removedThis = arguments.Skip(1).Reverse().ToArray();
            return nativeFunction(this, removedThis);
        }

        var callablePropertyCode = callableProperty.Code();

        var newCallFrame = new Frame(frame, callableProperty, false, callablePropertyCode.IsAsync);
        for (var i = 1; i <= arg; i++)
            newCallFrame.PushOperand(arguments[i]);

        // Set this
        newCallFrame.SetEnvVar(0, zsObject);

        callablePropertyCode.MergeCaptureToEnvironment(newCallFrame);

        return callablePropertyCode.ArgCount != arg
            ? ZsValue.FromErrorMessage(
                ErrorClass,
                $"arg mismatch {callablePropertyCode.ArgCount} != {arg}",
                BuildTracebackFromFrame()
            )
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

                var zsFuture =
                    current.Future ?? ZsValue.FromFuture(new Future(FutureState.Rejected, current));
                current.SetFutureOrSkip(zsFuture);

                zsFuture.Future().Reject(errorValue, null);

                PendingTasks.Enqueue(zsFuture);

                CurrentFrame = current.CallerFrame;
                return;
            }

            current.Terminate();
            current = current.CallerFrame;
        }

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

            if (midIndex == pc)
                return debugLines[mid];

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

        var currentFrame = CurrentFrame;
        while (currentFrame != null)
        {
            var code = currentFrame.FunctionValue.Code();
            var tracebackLine = GetLine(code.DebugLines, currentFrame.Pc);
            var moduleName = State.ModuleNames[tracebackLine.ModuleId];
            sites.Enqueue($"    at [{moduleName}:{code.Name}:{tracebackLine.Line}]");
            currentFrame = currentFrame.CallerFrame;
        }

        return string.Join(Environment.NewLine, sites);
    }

    public ZsValue Run(Frame frame)
    {
        CurrentFrame = frame;
        var code = frame.FunctionValue.Code();

        if (frame.PendingError != null)
        {
            var err = frame.PendingError;
            frame.PendingError = null;

            RaiseOrHandleException(frame, err);

            if (frame.Suspended)
                return frame.Future!;
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
                        RaiseOrHandleException(
                            frame,
                            ZsValue.FromErrorMessage(
                                ErrorClass,
                                "referenced before assignment",
                                BuildTracebackFromFrame()
                            )
                        );
                        break;
                    }

                    frame.PushOperand(val);
                    break;
                }
                case OpCode.LoadConst:
                {
                    var off = ReadInt(frame);
                    frame.Forward(4);
                    frame.PushOperand(State.Constants[off]);
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
                    frame.PushOperand(NullSingleton);
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
                case OpCode.ArrayUnpack:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    DoArrayUnpack(frame, size);
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
                case OpCode.GetAttr:
                case OpCode.GetAttrOrPopDup:
                {
                    var attr = ReadString(frame);
                    frame.Forward(attr.Length + 1);
                    var attribute = DoGetAttr(frame, attr, opcode == OpCode.GetAttrOrPopDup);
                    if (ZsValue.IsInstanceOf(attribute, "Error"))
                    {
                        RaiseOrHandleException(frame, attribute);
                        break;
                    }

                    frame.PushOperand(attribute);
                    break;
                }
                case OpCode.GetIndex:
                {
                    var index = DoGetIndex(frame);
                    if (ZsValue.IsInstanceOf(index, "Error"))
                    {
                        RaiseOrHandleException(frame, index);
                        break;
                    }

                    frame.PushOperand(index);
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

                    if (!frame.Suspended)
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

                    if (!frame.Suspended)
                        frame.PushOperand(ret);
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
                case OpCode.PopNTry:
                {
                    var size = ReadInt(frame);
                    frame.Forward(4);
                    for (var i = 0; i < size; i++)
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
                    if (!frame.PopOperand().Bool())
                        frame.JumpTo(jmp);
                    break;
                }
                case OpCode.PopJumpIfTrue:
                {
                    var jmp = ReadInt(frame);
                    frame.Forward(4);
                    if (frame.PopOperand().Bool())
                        frame.JumpTo(jmp);
                    break;
                }
                case OpCode.Jump:
                case OpCode.AbsJump:
                {
                    var jmp = ReadInt(frame);
                    frame.JumpTo(jmp);
                    break;
                }
                case OpCode.Return:
                {
                    Debug.Assert(
                        frame.GetOperandCount() == 1,
                        $"{code.Name} -> frame.GetOperandCount()({frame.GetOperandCount()}) != 1"
                    );
                    Debug.Assert(
                        frame.GetTryTableCount() == 0,
                        $"{code.Name} -> frame.GetTryTableCount()({frame.GetTryTableCount()}) != 0"
                    );

                    CurrentFrame = CurrentFrame.CallerFrame;

                    if (frame.Asynchronous || frame.Future != null)
                    {
                        var zsFuture =
                            frame.Future != null!
                                ? frame.Future
                                : ZsValue.FromFuture(new Future(FutureState.Fulfill, frame, null!));

                        frame.SetFutureOrSkip(zsFuture);

                        zsFuture
                            .Future()
                            .FullFill(frame.PopOperand(), frame.IsCallback ? null : PendingTasks);

                        if (frame.IsCallback)
                            _deferredTasks.Enqueue(zsFuture);

                        return zsFuture;
                    }

                    var value = frame.PopOperand();
                    frame.Dispose();
                    return value;
                }
                default:
                {
                    throw new InvalidSwitchValueException($"OpCode {opcode} not implemented");
                }
            }
        }

        // 1 := fail
        return frame.Future ?? ZsValue.FromInt(1);
    }

    public void MainLoop(ZsValue globalCodeObject)
    {
        var globalFrame = new Frame(null, globalCodeObject, true, false);
        State.AutoLoader.InjectObject(this, globalFrame);

        Run(globalFrame);

        while (PendingTasks.Count > 0 || _deferredTasks.Count > 0)
        {
            if (PendingTasks.Count == 0)
                while (_deferredTasks.Count > 0)
                    PendingTasks.Enqueue(_deferredTasks.Dequeue());

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