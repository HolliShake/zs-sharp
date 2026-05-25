using System.Text;

namespace zscript;

public class Lexer(string path, string source)
{
    private static readonly HashSet<string> Keywords =
    [
        "class", "fn", "async", "await", "if", "switch",
        "base", "for", "while", "do", "print"
    ];

    private int _colm = 1;
    private int _indx;
    private int _line = 1;

    protected Token Next()
    {
        while (_indx < source.Length)
        {
            var c = source[_indx];
            if (char.IsWhiteSpace(c))
            {
                Advance();
                continue;
            }

            if (char.IsLetter(c) || c == '_') return ReadIdentifierOrKeyword();

            if (char.IsDigit(c)) return ReadNumber();

            if (c == '"') return ReadString();

            var symbol = TryReadOperator();
            if (symbol != null) return symbol;
        }

        return new Token(TokenType.Eof, "", new Position(_line, _colm));
    }

    private void Advance()
    {
        if (_indx > source.Length) return;

        if (source[_indx] == '\n')
        {
            _line++;
            _colm = 1;
        }
        else
        {
            _colm++;
        }

        _indx++;
    }

    private Token ReadIdentifierOrKeyword()
    {
        var startPos = _indx;
        var startLine = _line;
        var startColumn = _colm;

        while (_indx < source.Length && (char.IsLetterOrDigit(source[_indx]) || source[_indx] == '_')) Advance();

        var name = source[startPos.._indx];
        var type = Keywords.Contains(name) ? TokenType.Key : TokenType.Idn;
        return new Token(type, name, new Position(startLine, startColumn));
    }

    private Token ReadNumber()
    {
        var startPos = _indx;
        var startLine = _line;
        var startColumn = _colm;

        if (_indx + 2 <= source.Length && source[_indx] == '0' && source[_indx + 1] is 'x' or 'X')
        {
            Advance();
            Advance();
            while (_indx < source.Length && IsHexDigit(source[_indx]))
                Advance();
            var value = source[startPos.._indx];
            return new Token(TokenType.Num, value, new Position(startLine, startColumn));
        }

        if (_indx + 2 <= source.Length && source[_indx] == '0' && source[_indx + 1] is 'b' or 'B')
        {
            Advance();
            Advance();
            while (_indx < source.Length && source[_indx] is '0' or '1')
                Advance();
            var value = source[startPos.._indx];
            return new Token(TokenType.Num, value, new Position(startLine, startColumn));
        }

        if (_indx + 2 <= source.Length && source[_indx] == '0' && source[_indx + 1] is 'o' or 'O')
        {
            Advance();
            Advance();
            while (_indx < source.Length && source[_indx] is >= '0' and <= '7')
                Advance();
            var value = source[startPos.._indx];
            return new Token(TokenType.Num, value, new Position(startLine, startColumn));
        }

        var hasDot = false;
        while (_indx < source.Length && char.IsDigit(source[_indx])) Advance();

        if (_indx < source.Length && source[_indx] == '.')
        {
            hasDot = true;
            Advance();
            while (_indx < source.Length && char.IsDigit(source[_indx]))
                Advance();
        }

        if (_indx < source.Length && source[_indx] is 'e' or 'E')
        {
            Advance();
            if (_indx < source.Length && source[_indx] is '+' or '-')
                Advance();
            while (_indx < source.Length && char.IsDigit(source[_indx]))
                Advance();
            hasDot = true;
        }

        var number = source[startPos.._indx];
        return new Token(hasDot ? TokenType.Num : TokenType.Int, number, new Position(startLine, startColumn));
    }

    private static bool IsHexDigit(char c)
    {
        return c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
    }

    private Token? TryReadOperator()
    {
        var startLine = _line;
        var startColumn = _colm;
        var c = source[_indx];

        switch (c)
        {
            case '(':
            {
                Advance();
                return new Token(TokenType.Sym, "(", new Position(startLine, startColumn));
            }
            case ')':
            {
                Advance();
                return new Token(TokenType.Sym, ")", new Position(startLine, startColumn));
            }
            case '[':
            {
                Advance();
                return new Token(TokenType.Sym, "[", new Position(startLine, startColumn));
            }
            case ']':
            {
                Advance();
                return new Token(TokenType.Sym, "]", new Position(startLine, startColumn));
            }
            case '{':
            {
                Advance();
                return new Token(TokenType.Sym, "{", new Position(startLine, startColumn));
            }
            case '}':
            {
                Advance();
                return new Token(TokenType.Sym, "}", new Position(startLine, startColumn));
            }
            case ',':
            {
                Advance();
                return new Token(TokenType.Sym, ",", new Position(startLine, startColumn));
            }
            case ';':
            {
                Advance();
                return new Token(TokenType.Sym, ";", new Position(startLine, startColumn));
            }
            case '+':
            {
                Advance();
                if (Match('+')) return new Token(TokenType.Sym, "++", new Position(startLine, startColumn));
                return Match('=')
                    ? new Token(TokenType.Sym, "+=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "+", new Position(startLine, startColumn));
            }
            case '-':
            {
                Advance();
                if (Match('-')) return new Token(TokenType.Sym, "--", new Position(startLine, startColumn));
                if (Match('>')) return new Token(TokenType.Sym, "->", new Position(startLine, startColumn));
                return Match('=')
                    ? new Token(TokenType.Sym, "-=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "-", new Position(startLine, startColumn));
            }
            case '*':
            {
                Advance();
                return Match('=')
                    ? new Token(TokenType.Sym, "*=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "*", new Position(startLine, startColumn));
            }
            case '/':
            {
                Advance();
                return Match('=')
                    ? new Token(TokenType.Sym, "/=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "/", new Position(startLine, startColumn));
            }
            case '%':
            {
                Advance();
                return Match('=')
                    ? new Token(TokenType.Sym, "%=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "%", new Position(startLine, startColumn));
            }
            case '<':
            {
                Advance();
                if (Match('<'))
                    return Match('=')
                        ? new Token(TokenType.Sym, "<<=", new Position(startLine, startColumn))
                        : new Token(TokenType.Sym, "<<", new Position(startLine, startColumn));
                return Match('=')
                    ? new Token(TokenType.Sym, "<=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "<", new Position(startLine, startColumn));
            }
            case '>':
            {
                Advance();
                if (Match('>'))
                    return Match('=')
                        ? new Token(TokenType.Sym, ">>=", new Position(startLine, startColumn))
                        : new Token(TokenType.Sym, ">>", new Position(startLine, startColumn));
                return Match('=')
                    ? new Token(TokenType.Sym, ">=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, ">", new Position(startLine, startColumn));
            }
            case '.':
            {
                Advance();
                if (Match('.') && Match('.'))
                    return new Token(TokenType.Sym, "...", new Position(startLine, startColumn));

                return new Token(TokenType.Sym, ".", new Position(startLine, startColumn));
            }

            case '&':
            {
                Advance();
                if (Match('&')) return new Token(TokenType.Sym, "&&", new Position(startLine, startColumn));
                if (Match('=')) return new Token(TokenType.Sym, "&=", new Position(startLine, startColumn));

                ErrorHandler.CompileError(path, source, "Unexpected '&'. Expected '&&' or '&='.",
                    new Position(startLine, startColumn));
                break;
            }

            case '|':
            {
                Advance();
                if (Match('|')) return new Token(TokenType.Sym, "||", new Position(startLine, startColumn));
                if (Match('=')) return new Token(TokenType.Sym, "|=", new Position(startLine, startColumn));

                ErrorHandler.CompileError(path, source, "Unexpected '|'. Expected '||' or '|='.",
                    new Position(startLine, startColumn));
                break;
            }

            case '=':
            {
                Advance();
                return Match('=')
                    ? new Token(TokenType.Sym, "==", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "=", new Position(startLine, startColumn));
            }
            case '!':
            {
                Advance();
                return Match('=')
                    ? new Token(TokenType.Sym, "!=", new Position(startLine, startColumn))
                    : new Token(TokenType.Sym, "!", new Position(startLine, startColumn));
            }
            default:
            {
                ErrorHandler.CompileError(path, source, $"Unknown operator or symbol '{c}' (U+{(int)c:X4}).",
                    new Position(startLine, startColumn));
                break;
            }
        }

        return null;
    }

    private bool Match(char expected)
    {
        if (_indx >= source.Length || source[_indx] != expected)
            return false;
        _indx++;
        return true;
    }

    private Token ReadString()
    {
        var startLine = _line;
        var startColumn = _colm;
        Advance();
        var str = new StringBuilder();

        while (_indx < source.Length)
        {
            var c = source[_indx];
            if (c == '\\')
            {
                Advance();
                if (_indx >= source.Length) break;
                var next = source[_indx];
                switch (next)
                {
                    case '"': str.Append('"'); break;
                    case '\\': str.Append('\\'); break;
                    case 'n': str.Append('\n'); break;
                    case 'r': str.Append('\r'); break;
                    case 't': str.Append('\t'); break;
                    default: str.Append(next); break;
                }

                Advance();
            }
            else if (c == '"')
            {
                Advance();
                break;
            }
            else
            {
                str.Append(c);
                Advance();
            }
        }

        return new Token(TokenType.Str, str.ToString(), new Position(startLine, startColumn));
    }
}