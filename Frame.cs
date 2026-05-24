namespace zscript;

public class Frame
{
    public readonly bool Asynchronous;
    public readonly Frame? CallerFrame;
    public readonly int CodeLen;
    public readonly Cell[] Environment;
    public readonly ZsValue FunctionValue;
    public readonly Stack<ZsValue> Operands;
    public ZsValue? Future;
    public bool IsFaulted;
    public readonly bool IsCallback;
    public int Pc;

    public Frame(Frame? callerFrame, ZsValue functionValue, bool callback, bool asynchronous)
    {
        var code = functionValue.Code();
        CallerFrame = callerFrame;
        Operands = [];
        FunctionValue = functionValue;
        Asynchronous = asynchronous;
        Pc = 0;
        Future = null;
        CodeLen = code.Bytecode.Count;
        Suspended = false;
        Environment = new Cell[code.LocalCount];
        IsFaulted = false;
        IsCallback = false;
        for (var i = 0; i < code.LocalCount; i++) Environment[i] = new Cell();
    }

    public bool Suspended { get; private set; }

    public void SetFutureOrSkip(ZsValue future)
    {
        Future ??= future;
    }

    public void Suspend()
    {
        Suspended = true;
    }

    public void Wake()
    {
        Suspended = false;
    }

    public void Forward(int pc)
    {
        Pc += pc;
    }

    public void PushOperand(ZsValue value)
    {
        Operands.Push(value);
    }

    public ZsValue PopOperand()
    {
        return Operands.Pop();
    }

    public ZsValue PeekOperandAt(int address)
    {
        return Operands.ElementAt(address);
    }

    public ZsValue? GetEnvVar(int address)
    {
        return Environment[address].Value;
    }

    public void SetEnvVar(int address, ZsValue value)
    {
        Environment[address].Value = value;
    }
}