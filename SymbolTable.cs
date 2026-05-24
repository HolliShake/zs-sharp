using System.Diagnostics;

namespace zscript;

public class SymbolTable
{
    public readonly SymbolTable? Parent;
    public readonly ScopeType ScopeType;
    public readonly Dictionary<string, Symbol> Symbols = new();

    public SymbolTable(ScopeType scopeType, SymbolTable? parent)
    {
        ScopeType = scopeType;
        Parent = parent;
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

            if (boundary.HasValue && current.ScopeType == boundary.Value)
                break;

            current = current.Parent;
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
            if (current.Symbols.TryGetValue(symbol, out var found))
                return new LookupDetail(found, depth, isLocalToFunction);

            // 2. If the current scope IS the boundary, any parent scope will be non-local
            if (isLocalToFunction && current.ScopeType == boundary) isLocalToFunction = false;

            // 3. Traverse up the environment chain
            current = current.Parent;
            ++depth;
        }

        throw new KeyNotFoundException($"Symbol '{symbol}' not found.");
    }

    public void Add(string symbol, int offset, bool constant, Position position)
    {
        Debug.Assert(!Symbols.ContainsKey(symbol), $"Symbol {symbol} already exists");
        Symbols[symbol] = new Symbol(symbol, offset, constant, position);
    }
}