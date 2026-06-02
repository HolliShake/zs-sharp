namespace obiwan;

public class OpCodeDebug(int moduleId, int index, int line)
{
    public int ModuleId { get; } = moduleId;
    public int Index { get; } = index;
    public int Line { get; } = line;
}