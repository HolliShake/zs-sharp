using System.Text;

namespace zscript;

public class Frame(Frame? callerFrame, ZsValue functionValue, bool callback, bool asynchronous)
{
    private readonly Stack<ZsValue> _operands = [];

    public readonly bool Asynchronous = asynchronous;
    public readonly Frame? CallerFrame = callerFrame;
    public readonly int CodeLen = functionValue.Code().Bytecode.Count;
    public readonly Cell[] Environment = BuildEnvironment(functionValue.Code());
    public readonly ZsValue FunctionValue = functionValue;
    public readonly bool IsCallback = callback; // bug fix: was overwritten to false unconditionally
    public readonly Stack<TryBlock> TryCatchTable = [];
    public ZsValue? Future;
    public bool IsFaulted;
    public int Pc;
    public ZsValue? PendingError;

    public bool Suspended { get; private set; }

    private static Cell[] BuildEnvironment(Code code)
    {
        var env = new Cell[code.LocalCount];
        for (var i = 0; i < code.LocalCount; i++) env[i] = new Cell(null);
        return env;
    }

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

    public void JumpTo(int pc)
    {
        Pc = pc;
    }

    public void PushOperand(ZsValue value)
    {
        _operands.Push(value);
    }

    public ZsValue PopOperand()
    {
        return _operands.Pop();
    }

    public ZsValue PeekOperand()
    {
        return _operands.Peek();
    }

    public void PopOperand(int size)
    {
        for (var i = 0; i < size; i++) _operands.Pop();
    }

    public ZsValue PeekOperandAt(int address)
    {
        return _operands.ElementAt(address);
    }

    public ZsValue? GetEnvVar(int address)
    {
        return Environment[address].Value;
    }

    public void SetEnvVar(int address, ZsValue value)
    {
        Environment[address].Value = value;
    }

    public void PushTryTable(TryBlock tryBlock)
    {
        TryCatchTable.Push(tryBlock);
    }

    public TryBlock PopTryTable()
    {
        return TryCatchTable.Pop();
    }

    public TryBlock PeekTryTable()
    {
        return TryCatchTable.Peek();
    }

    public bool HasTryHandler()
    {
        return TryCatchTable.Count > 0;
    }

    public void Terminate()
    {
        Suspended = true;
        Pc = CodeLen;
    }

    public void DumpOperand()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        foreach (var op in _operands)
        {
            sb.Append(op);
            sb.Append(',');
        }

        sb.Append(']');
        Console.WriteLine(sb.ToString());
    }

    public int GetOperandCount()
    {
        return _operands.Count;
    }
}