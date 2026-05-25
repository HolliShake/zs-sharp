using System.Diagnostics;

namespace zscript;

public class SymbolTable(ScopeType scopeType, SymbolTable? parent)
{
    private readonly SymbolTable? _parent = parent;
    private readonly ScopeType _scopeType = scopeType;
    private readonly Dictionary<string, Symbol> _symbols = new();

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
            // 1. Check if the symbol is in the current scope
            if (current._symbols.TryGetValue(symbol, out var found))
                return new LookupDetail(found, depth, isLocalToFunction);

            // 2. If the current scope IS the boundary, any parent scope will be non-local
            if (isLocalToFunction && current._scopeType == boundary) isLocalToFunction = false;

            // 3. Traverse up the environment chain
            current = current._parent;
            ++depth;
        }

        throw new KeyNotFoundException($"Symbol '{symbol}' not found.");
    }

    public void Add(string symbol, int offset, bool constant, Position position)
    {
        Debug.Assert(!_symbols.ContainsKey(symbol), $"Symbol {symbol} already exists");
        _symbols[symbol] = new Symbol(symbol, offset, constant, position);
    }
}