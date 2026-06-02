using System.Text;

namespace obiwan;

public class Frame(Frame? callerFrame, ObValue functionValue, bool callback, bool asynchronous) : IDisposable
{
    private readonly Stack<ObValue> _operands = [];
    private readonly Dictionary<string, ObValue> _providedNames = [];
    private readonly Stack<TryBlock> _tryCatchTable = [];
    public readonly bool Asynchronous = asynchronous;
    public readonly Frame? CallerFrame = callerFrame;
    public readonly int CodeLen = functionValue.Code().Bytecode.Count;
    public readonly Cell[] Environment = BuildEnvironment(functionValue.Code());
    public readonly ObValue FunctionValue = functionValue;
    public readonly bool IsCallback = callback; // bug fix: was overwritten to false unconditionally
    public ObValue? Future;
    public int Pc;
    public ObValue? PendingError;
    public bool Suspended { get; private set; }

    public void Dispose()
    {
        _operands.Clear();
        _tryCatchTable.Clear();
        _providedNames.Clear();
        Future = null;
        Pc = 0;
        PendingError = null;
        Suspended = false;
        for (var i = 0; i < Environment.Length; i++)
        {
            if (Environment[i].IsRef) continue;
            Environment[i].Value = null;
            Environment[i] = null!;
        }
    }

    public void AddProvidedNamespace(string ns, ObValue module)
    {
        _providedNames[ns] = module;
    }

    public ObValue? GetProvidedNamespace(string ns)
    {
        var currentFrame = CallerFrame;
        while (currentFrame != null)
        {
            if (currentFrame._providedNames.TryGetValue(ns, out var module)) return module;
            currentFrame = currentFrame.CallerFrame;
        }

        return null;
    }

    private static Cell[] BuildEnvironment(Code code)
    {
        var env = new Cell[code.LocalCount];
        for (var i = 0; i < code.LocalCount; i++) env[i] = new Cell(null);
        return env;
    }

    public void SetFutureOrSkip(ObValue future)
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

    public void PushOperand(ObValue value)
    {
        _operands.Push(value);
    }

    public ObValue PopOperand()
    {
        return _operands.Pop();
    }

    public ObValue PeekOperand()
    {
        return _operands.Peek();
    }

    public void PopOperand(int size)
    {
        for (var i = 0; i < size; i++) _operands.Pop();
    }

    public ObValue PeekOperandAt(int address)
    {
        return _operands.ElementAt(address);
    }

    public ObValue? GetEnvVar(int address)
    {
        return Environment[address].Value;
    }

    public void SetEnvVar(int address, ObValue value)
    {
        Environment[address].Value = value;
    }

    public void PushTryTable(TryBlock tryBlock)
    {
        _tryCatchTable.Push(tryBlock);
    }

    public TryBlock PopTryTable()
    {
        return _tryCatchTable.Pop();
    }

    public TryBlock PeekTryTable()
    {
        return _tryCatchTable.Peek();
    }

    public bool HasTryHandler()
    {
        return _tryCatchTable.Count > 0;
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

    public int GetTryTableCount()
    {
        return _tryCatchTable.Count;
    }
}