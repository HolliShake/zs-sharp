using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace zscript;

public class Code(string name, int argCount, bool isAsync)
{
    public List<OpCodeDebug> DebugLines = [];
    public string Name { get; } = name;
    public int ArgCount { get; } = argCount;
    public bool IsAsync { get; } = isAsync;

    public List<byte> Bytecode = [];
    public Dictionary<int, Cell> CapturedCells = [];

    public List<(int Depth, int Address, int Destination)> Captures = [];

    public int LocalCount { get; private set; }

    public Code Clone()
    {
        return new Code(Name, ArgCount, IsAsync)
        {
            DebugLines = [.. DebugLines],
            Bytecode   = [.. Bytecode],
            Captures   = [.. Captures],
            LocalCount = LocalCount,
            CapturedCells = CapturedCells.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            )
        };
    }

    public int AllocateLocal()
    {
        return LocalCount++;
    }

    public void EmitLine(int moduleId, int line)
    {
        var index = Bytecode.Count;
        DebugLines.Add(new OpCodeDebug(moduleId, index, line));
    }

    public void Emit(OpCode opcode)
    {
        Bytecode.Add((byte)opcode);
    }

    public void Emit(OpCode opcode, int value)
    {
        Bytecode.Add((byte)opcode);
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        Bytecode.AddRange(buffer);
    }

    public void Emit(OpCode opcode, double value)
    {
        Bytecode.Add((byte)opcode);
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        Bytecode.AddRange(buffer);
    }

    public void Emit(OpCode opcode, string value)
    {
        Bytecode.Add((byte)opcode);
        var strBytes = Encoding.UTF8.GetBytes(value);
        Span<byte> buffer = stackalloc byte[strBytes.Length];
        strBytes.CopyTo(buffer);
        Bytecode.AddRange(buffer);
        Bytecode.Add(0);
    }

    public int EmitJump(OpCode opcode)
    {
        var current = Bytecode.Count;
        Emit(opcode, 0);
        return current + 1;
    }

    public void Label(int placeholderAddress)
    {
        var jumpDestination = Bytecode.Count;
        var span = CollectionsMarshal.AsSpan(Bytecode).Slice(placeholderAddress, 4);
        BinaryPrimitives.WriteInt32BigEndian(span, jumpDestination);
    }

    public void AddCapture((int Depth, int Address, int Destination) capture)
    {
        Captures.Add(capture);
    }

    public void MergeCaptureToEnvironment(Frame frame)
    {
        foreach (var (key, cell) in CapturedCells) frame.Environment[key] = cell;
    }
}