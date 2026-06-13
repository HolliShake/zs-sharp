using System.Runtime.InteropServices;
using System.Text;

namespace obiwan;

/// <summary>
///     Describes a single decoded bytecode instruction.
/// </summary>
public record DecompiledInstruction(
    int Offset,
    OpCode OpCode,
    string Mnemonic,
    string? Operand,
    int? JumpTarget
)
{
    public override string ToString()
    {
        var operandPart = Operand is not null ? $"  {Operand}" : string.Empty;
        return $"{Offset,6}  {Mnemonic,-22}{operandPart}";
    }
}

/// <summary>
///     Decompiles a single <see cref="Code" /> object into a list of
///     <see cref="DecompiledInstruction" /> records, and can pretty-print the
///     entire <see cref="State" /> (all code objects + constants).
/// </summary>
public static class Decompiler
{
    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    ///     Decompiles every code object in <paramref name="state" /> and writes the
    ///     result to <paramref name="writer" /> (defaults to stdout).
    /// </summary>
    public static void Disassemble(State state, TextWriter? writer = null)
    {
        writer ??= Console.Out;

        writer.WriteLine("=== Constants ===");
        for (var i = 0; i < state.Constants.Count; i++)
            writer.WriteLine($"  [{i,4}]  {state.Constants[i]}");

        writer.WriteLine();
        writer.WriteLine("=== Modules ===");
        for (var i = 0; i < state.ModuleNames.Count; i++)
            writer.WriteLine($"  [{i,4}]  {state.ModuleNames[i]}");

        writer.WriteLine();
        writer.WriteLine("=== Code Objects ===");
        for (var i = 0; i < state.Codes.Count; i++)
        {
            writer.WriteLine();
            writer.WriteLine($"--- Code[{i}] ---");
            DisassembleCode(state.Codes[i], state, writer);
        }
    }

    /// <summary>
    ///     Decompiles a single <see cref="Code" /> object and writes the result to
    ///     <paramref name="writer" />.
    /// </summary>
    public static void DisassembleCode(Code code, State? state, TextWriter? writer = null)
    {
        writer ??= Console.Out;
        var instructions = Decode(code, state);

        writer.WriteLine($"  Name      : {code.Name}");
        writer.WriteLine($"  ArgCount  : {code.ArgCount}");
        writer.WriteLine($"  IsAsync   : {code.IsAsync}");
        writer.WriteLine($"  BytecodeLen: {code.Bytecode.Count}");

        if (code.Captures.Count > 0)
        {
            writer.WriteLine("  Captures  :");
            foreach (var (depth, address, destination, definedInLoop) in code.Captures)
                writer.WriteLine(
                    $"             depth={depth}  addr={address}  dest={destination} loop={definedInLoop}");
        }

        writer.WriteLine();
        writer.WriteLine("  Offset  Mnemonic               Operand");
        writer.WriteLine("  " + new string('-', 60));

        // Build a set of jump targets so we can annotate them with labels.
        var jumpTargets = new HashSet<int>(
            instructions
                .Where(i => i.JumpTarget.HasValue)
                .Select(i => i.JumpTarget!.Value)
        );

        // Build a mapping from offset to source line (for annotation).
        var lineMap = BuildLineMap(code);

        var lastLine = -1;

        foreach (var instr in instructions)
        {
            // Emit source-line comment when the line changes.
            if (lineMap.TryGetValue(instr.Offset, out var line) && line != lastLine)
            {
                writer.WriteLine($"  ; line {line}");
                lastLine = line;
            }

            // Emit a label if this offset is a jump target.
            if (jumpTargets.Contains(instr.Offset))
                writer.WriteLine($"L{instr.Offset}:");

            writer.WriteLine("  " + instr);
        }

        writer.WriteLine();
    }

    /// <summary>
    ///     Decodes the bytecode of <paramref name="code" /> into a flat list of
    ///     <see cref="DecompiledInstruction" /> objects.
    /// </summary>
    public static List<DecompiledInstruction> Decode(Code code, State? state = null)
    {
        var result = new List<DecompiledInstruction>();
        var bytecode = code.Bytecode;
        var pc = 0;

        while (pc < bytecode.Count)
        {
            var instrOffset = pc;
            var opcode = (OpCode)bytecode[pc++];

            DecompiledInstruction instruction;

            switch (opcode)
            {
                // ── Instructions with a 4-byte integer operand ──────────────

                case OpCode.LoadLocal:
                case OpCode.LoadCapture:
                {
                    var off = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = Make(instrOffset, opcode, $"var[{off}]");
                    break;
                }
                case OpCode.LoadConst:
                {
                    var off = ReadInt(bytecode, pc);
                    pc += 4;
                    var constRepr = state is not null && off < state.Constants.Count
                        ? FormatConst(state.Constants[off])
                        : $"const[{off}]";
                    instruction = Make(instrOffset, opcode, constRepr);
                    break;
                }
                case OpCode.LoadFunction:
                {
                    var off = ReadInt(bytecode, pc);
                    pc += 4;
                    var name = state is not null && off < state.Codes.Count
                        ? $"code[{off}] <{state.Codes[off].Name}>"
                        : $"code[{off}]";
                    instruction = Make(instrOffset, opcode, name);
                    break;
                }
                case OpCode.MakeArray:
                case OpCode.ArrayUnpack:
                case OpCode.MakeObject:
                {
                    var size = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = Make(instrOffset, opcode, $"size={size}");
                    break;
                }
                case OpCode.StoreName:
                case OpCode.StoreLocal:
                {
                    var off = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = Make(instrOffset, opcode, $"var[{off}]");
                    break;
                }
                case OpCode.Call:
                case OpCode.CallMethod:
                {
                    var arg = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = Make(instrOffset, opcode, $"argc={arg}");
                    break;
                }
                case OpCode.Print:
                {
                    var size = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = Make(instrOffset, opcode, $"argc={size}");
                    break;
                }
                case OpCode.PopNTry:
                {
                    var size = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = Make(instrOffset, opcode, $"count={size}");
                    break;
                }

                // ── Jump instructions with a 4-byte absolute target ─────────

                case OpCode.SetupTry:
                case OpCode.JumpIfFalseOrPop:
                case OpCode.JumpIfTrueOrPop:
                case OpCode.PopJumpIfFalse:
                case OpCode.PopJumpIfTrue:
                case OpCode.Jump:
                case OpCode.AbsJump:
                {
                    var target = ReadInt(bytecode, pc);
                    pc += 4;
                    instruction = MakeJump(instrOffset, opcode, target);
                    break;
                }

                // ── Instructions with a null-terminated UTF-8 string ────────

                case OpCode.LoadString:
                {
                    var str = ReadNullTerminatedString(bytecode, pc, out var advance);
                    pc += advance;
                    instruction = Make(instrOffset, opcode, QuoteString(str));
                    break;
                }
                case OpCode.GetAttr:
                case OpCode.GetAttrOrPopDup:
                {
                    var attr = ReadNullTerminatedString(bytecode, pc, out var advance);
                    pc += advance;
                    instruction = Make(instrOffset, opcode, $".{attr}");
                    break;
                }

                // ── No-operand instructions ──────────────────────────────────

                case OpCode.LoadNull:
                case OpCode.GetIndex:
                case OpCode.DupTop:
                case OpCode.Await:
                case OpCode.PopTop:
                case OpCode.PopTry:
                case OpCode.Return:
                case OpCode.BinMul:
                case OpCode.BinDiv:
                case OpCode.BinMod:
                case OpCode.BinAdd:
                case OpCode.BinSub:
                case OpCode.BinLshift:
                case OpCode.BinRshift:
                case OpCode.CmpLt:
                case OpCode.CmpLe:
                case OpCode.CmpGt:
                case OpCode.CmpGe:
                case OpCode.CmpEq:
                case OpCode.CmpNe:
                case OpCode.BinAnd:
                case OpCode.BinOr:
                case OpCode.BinXor:
                {
                    instruction = Make(instrOffset, opcode, null);
                    break;
                }

                default:
                {
                    // Unknown opcode: emit a raw byte and keep going.
                    instruction = new DecompiledInstruction(
                        instrOffset,
                        opcode,
                        $"UNKNOWN(0x{(byte)opcode:X2})",
                        null,
                        null
                    );
                    break;
                }
            }

            result.Add(instruction);
        }

        return result;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DecompiledInstruction Make(int offset, OpCode op, string? operand)
    {
        return new DecompiledInstruction(offset, op, op.ToString(), operand, null);
    }

    private static DecompiledInstruction MakeJump(int offset, OpCode op, int target)
    {
        return new DecompiledInstruction(offset, op, op.ToString(), $"-> L{target}", target);
    }

    private static int ReadInt(List<byte> bytecode, int pc)
    {
        var b0 = bytecode[pc];
        var b1 = bytecode[pc + 1];
        var b2 = bytecode[pc + 2];
        var b3 = bytecode[pc + 3];
        return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
    }

    private static string ReadNullTerminatedString(List<byte> bytecode, int pc, out int advance)
    {
        var end = pc;
        while (end < bytecode.Count && bytecode[end] != 0)
            end++;

        var length = end - pc;
        advance = length + 1; // include the null terminator

        if (length == 0)
            return string.Empty;

        ReadOnlySpan<byte> span = CollectionsMarshal.AsSpan(bytecode).Slice(pc, length);
        return Encoding.UTF8.GetString(span);
    }

    /// <summary>
    ///     Builds a pc-to-source-line dictionary from the <see cref="Code.DebugLines" />
    ///     table using the same binary-search logic as the VM's <c>GetLine</c> helper.
    ///     We emit the line number at the first instruction that belongs to it.
    /// </summary>
    private static Dictionary<int, int> BuildLineMap(Code code)
    {
        var map = new Dictionary<int, int>();
        if (code.DebugLines.Count == 0)
            return map;

        // Walk every instruction offset and record the first offset for each
        // distinct (source) line.
        var lastLine = -1;
        var pc = 0;
        while (pc < code.Bytecode.Count)
        {
            var line = GetLineFast(code.DebugLines, pc);
            if (line != lastLine)
            {
                map[pc] = line;
                lastLine = line;
            }

            // Advance by 1 (we only need to probe once per instruction start,
            // but the caller does that correctly via Decode()).
            pc++;
        }

        return map;
    }

    private static int GetLineFast(List<OpCodeDebug> debugLines, long pc)
    {
        var low = 0;
        var high = debugLines.Count - 1;
        var best = 0;

        while (low <= high)
        {
            var mid = low + ((high - low) >> 1);
            var midIdx = debugLines[mid].Index;

            if (midIdx == pc)
                return debugLines[mid].Line;

            if (midIdx < pc)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return debugLines[best].Line;
    }

    private static string FormatConst(ObValue v)
    {
        return v.Type switch
        {
            ValueType.String => QuoteString(v.String()),
            ValueType.Number => v.Number().ToString("G"),
            ValueType.Int => v.Int().ToString(),
            ValueType.Bool => v.Bool() ? "true" : "false",
            ValueType.Null => "null",
            _ => v.ToString() ?? "?"
        };
    }

    private static string QuoteString(string s)
    {
        // Escape control characters and quotes for readability.
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var ch in s)
            sb.Append(ch switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => ch.ToString()
            });
        sb.Append('"');
        return sb.ToString();
    }

    public static string GenerateString(Code code, State? state = null)
    {
        return string.Join("\n", Decode(code, state));
    }
}