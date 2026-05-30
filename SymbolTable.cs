using System.Diagnostics;

namespace zscript;

public class SymbolTable(ScopeType scopeType, SymbolTable? parent)
{
    private readonly SymbolTable? _parent = parent;
    private readonly ScopeType _scopeType = scopeType;
    private readonly Dictionary<string, Symbol> _symbols = new();
    private readonly List<int> _returnSignals = [];

    public bool AlreadyExists(string symbol)
    {
        return _symbols.ContainsKey(symbol);
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
            if (current._symbols.TryGetValue(symbol, out var found))
                return new LookupDetail(found, depth, isLocalToFunction);

            if (isLocalToFunction && current._scopeType == boundary) isLocalToFunction = false;
            if (current._scopeType == boundary) ++depth;


            current = current._parent;
        }

        throw new KeyNotFoundException($"Symbol '{symbol}' not found.");
    }

    public void Add(string symbol, int offset, bool constant, Position position)
    {
        Debug.Assert(!_symbols.ContainsKey(symbol), $"Symbol {symbol} already exists");
        _symbols[symbol] = new Symbol(symbol, offset, constant, position);
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
    
    public void AddReturnSignal(int offset)
    {
        _returnSignals.Add(offset);
    }
    
    public IEnumerable<int> GetReturnSignals()
    {
        return _returnSignals;
    }
}