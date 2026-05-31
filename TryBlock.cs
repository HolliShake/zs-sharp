namespace zscript;

public class TryBlock(int fromPc, int toPc, int fromLine, int toLine)
{
    public readonly int ToPc = toPc;
    public int FromLine = fromLine;
    public int FromPc = fromPc;
    public int ToLine = toLine;
}