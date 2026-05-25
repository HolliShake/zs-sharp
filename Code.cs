using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;

namespace zscript;

public class Code(int argCount, bool isAsync)
{
    public int ArgCount { get; } = argCount;
    public bool IsAsync { get; } = isAsync;

    public List<byte> Bytecode { get; } = [];
    public Dictionary<int, Cell> CapturedCells { get; } = [];

    public List<(int Depth, int Address, int Destination)> Captures { get; } = [];

    public int LocalCount { get; private set; }

    public int AllocateLocal()
    {
        return LocalCount++;
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