namespace zscript;

public class TryBlock(int fromPc, int toPc, int fromLine, int toLine)
{
    public int FromPc = fromPc;
    public int ToPc = toPc;
    public int FromLine = fromLine;
    public int ToLine = toLine;
}