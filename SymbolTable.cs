using System.Diagnostics;

namespace obiwan;

public class SymbolTable(ScopeType scopeType, SymbolTable? parent)
{
    private readonly List<int> _breakSignals = [];
    private readonly List<int> _continueSignals = [];
    private readonly SymbolTable? _parent = parent;
    private readonly ScopeType _scopeType = scopeType;
    public readonly Dictionary<string, Symbol> Symbols = new();

    public bool ScopeIs(ScopeType scopeType)
    {
        return _scopeType == scopeType;
    }

    public bool IsInside(ScopeType scopeType)
    {
        return ScopeIs(scopeType) || (_parent != null && _parent.IsInside(scopeType));
    }

    public bool AlreadyExists(string symbol)
    {
        return Symbols.ContainsKey(symbol);
    }

    public bool SymbolExists(string symbol, ScopeType? boundary = null)
    {
        var current = this;

        while (current != null)
        {
            if (current.AlreadyExists(symbol))
                return true;

            if (boundary.HasValue && current._scopeType == boundary.Value)
                break;

            current = current._parent;
        }

        return false;
    }

    public LookupDetail Find(string symbol, ScopeType? boundary = null)
    {
        // Default boundary to Function if not provided
        boundary ??= ScopeType.Function;

        var depth = 0;
        var current = this;
        var isLocalToFunction = true;

        while (current != null)
        {
            if (current.Symbols.TryGetValue(symbol, out var found))
                return new LookupDetail(found, depth, isLocalToFunction);

            if (isLocalToFunction && current._scopeType == boundary) isLocalToFunction = false;
            if (current._scopeType == boundary) ++depth;


            current = current._parent;
        }

        throw new KeyNotFoundException($"Symbol '{symbol}' not found.");
    }

    public void Add(string symbol, int offset, bool constant, bool definedInLoop, Position position)
    {
        Debug.Assert(!Symbols.ContainsKey(symbol), $"Symbol {symbol} already exists");
        Symbols[symbol] = new Symbol(symbol, offset, constant, definedInLoop, position);
    }

    public static bool IsAncestorOf(SymbolTable? ancestor, SymbolTable? descendant)
    {
        if (ancestor == null || descendant == null) return false;
        if (ancestor == descendant) return false;

        var current = descendant._parent;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = current._parent;
        }

        return false;
    }

    public static int Depth(SymbolTable? stopPoint, SymbolTable? startPoint)
    {
        if (stopPoint == null || startPoint == null) return -1;
        if (stopPoint == startPoint) return 0;

        var current = startPoint._parent;
        var depth = 0;
        while (current != null)
        {
            if (current._scopeType == stopPoint._scopeType) return depth;
            current = current._parent;
            ++depth;
        }

        return depth;
    }

    public SymbolTable? GetNearestParent(params ScopeType[] types)
    {
        var current = this;
        while (current != null)
        {
            if (types.Contains(current._scopeType))
                return current;
            current = current._parent;
        }

        return null;
    }

    public void AddBreakSignal(int offset)
    {
        _breakSignals.Add(offset);
    }

    public void AddContinueSignal(int offset)
    {
        _continueSignals.Add(offset);
    }


    public IEnumerable<int> GetBreakSignals()
    {
        return _breakSignals;
    }

    public IEnumerable<int> GetContinueSignals()
    {
        return _continueSignals;
    }
}