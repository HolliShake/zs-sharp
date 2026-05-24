namespace zscript;

public class LookupDetail(Symbol symbol, int depth, bool isLocal)
{
    public Symbol Symbol { get; } = symbol;
    public int Depth { get; } = depth;
    public bool IsLocal { get; set; } = isLocal;
}