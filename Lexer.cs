using System.Text;

namespace obiwan;

public class Lexer(string path, string source)
{
    protected readonly string Path = path;
    protected readonly string Source = source;
    private int _colm = 1;
    private int _indx;
    private int _line = 1;

    protected Token Next()
    {
        while (_indx < Source.Length)
        {
            var rune = Rune.GetRuneAt(Source, _indx);

            if (Rune.IsWhiteSpace(rune))
            {
                Advance();
                continue;
            }

            if (Rune.IsLetter(rune) || Source[_indx] == '_') return ReadIdentifierOrKeyword();

            if (Rune.IsDigit(rune)) return ReadNumber();

            if (Source[_indx] == '"') return ReadString();

            if (Source[_indx] == '/' && _indx + 1 < Source.Length)
            {
                if (Source[_indx + 1] == '/')
                {
                    SkipLineComment();
                    continue;
                }

                if (Source[_indx + 1] == '*')
                {
                    SkipBlockComment();
                    continue;
                }
            }

            var symbol = TryReadOperator();
            if (symbol != null) return symbol;
        }

        return new Token(TokenType.Eof, "", new Position(_line, _colm));
    }

    private void Advance()
    {
        if (_indx >= Source.Length) return;

        if (Source[_indx] == '\n')
        {
            _line++;
            _colm = 1;
            _indx++;
        }
        else
        {
            var rune = Rune.GetRuneAt(Source, _indx);
            _colm++;
            _indx += rune.Utf16SequenceLength;
        }
    }

    private void SkipLineComment()
    {
        // Consume the leading '//'
        Advance();
        Advance();

        // Advance until end of line or end of source
        while (_indx < Source.Length && Source[_indx] != '\n')
            Advance();
    }

    private void SkipBlockComment()
    {
        var startLine = _line;
        var startColumn = _colm;

        // Consume the leading '/*'
        Advance();
        Advance();

        while (_indx < Source.Length)
        {
            if (Source[_indx] == '*' && _indx + 1 < Source.Length && Source[_indx + 1] == '/')
            {
                // Consume the closing '*/'
                Advance();
                Advance();
                return;
            }

            Advance();
        }

        // Reached end of source without finding '*/'
        ErrorHandler.CompileError(Path, Source, "Unterminated block comment.",
            new Position(startLine, startColumn));
    }

    private Token ReadIdentifierOrKeyword()
    {
        var startPos = _indx;
        var startLine = _line;
        var startColumn = _colm;

        while (_indx < Source.Length)
        {
            var rune = Rune.GetRuneAt(Source, _indx);
            if (!Rune.IsLetterOrDigit(rune) && Source[_indx] != '_') break;
            Advance();
        }

        var name = Source[startPos.._indx];
        var type = Keyword.IsKeyword(name) ? TokenType.Key : TokenType.Idn;
        return new Token(type, name, new Position(startLine, startColumn));
    }

    private Token ReadNumber()
    {
        var startPos = _indx;
        var startLine = _line;
        var startColumn = _colm;

        if (_indx + 2 <= Source.Length && Source[_indx] == '0' && Source[_indx + 1] is 'x' or 'X')
        {
            Advance();
            Advance();
            while (_indx < Source.Length && IsHexDigit(Source[_indx]))
                Advance();
            var value = Source[startPos.._indx];
            return new Token(TokenType.Num, value, new Position(startLine, startColumn));
        }

        if (_indx + 2 <= Source.Length && Source[_indx] == '0' && Source[_indx + 1] is 'b' or 'B')
        {
            Advance();
            Advance();
            while (_indx < Source.Length && Source[_indx] is '0' or '1')
                Advance();
            var value = Source[startPos.._indx];
            return new Token(TokenType.Num, value, new Position(startLine, startColumn));
        }

        if (_indx + 2 <= Source.Length && Source[_indx] == '0' && Source[_indx + 1] is 'o' or 'O')
        {
            Advance();
            Advance();
            while (_indx < Source.Length && Source[_indx] is >= '0' and <= '7')
                Advance();
            var value = Source[startPos.._indx];
            return new Token(TokenType.Num, value, new Position(startLine, startColumn));
        }

        var hasDot = false;
        while (_indx < Source.Length && Rune.IsDigit(Rune.GetRuneAt(Source, _indx))) Advance();

        if (_indx < Source.Length && Source[_indx] == '.')
        {
            hasDot = true;
            Advance();
            while (_indx < Source.Length && Rune.IsDigit(Rune.GetRuneAt(Source, _indx)))
                Advance();
        }

        if (_indx < Source.Length && Source[_indx] is 'e' or 'E')
        {
            Advance();
            if (_indx < Source.Length && Source[_indx] is '+' or '-')
                Advance();
            while (_indx < Source.Length && Rune.IsDigit(Rune.GetRuneAt(Source, _indx)))
                Advance();
            hasDot = true;
        }

        var number = Source[startPos.._indx];
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
        var c = Source[_indx];

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
            case ':':
            {
                Advance();
                return new Token(TokenType.Sym, ":", new Position(startLine, startColumn));
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

                ErrorHandler.CompileError(Path, Source, "Unexpected '&'. Expected '&&' or '&='.",
                    new Position(startLine, startColumn));
                break;
            }

            case '|':
            {
                Advance();
                if (Match('|')) return new Token(TokenType.Sym, "||", new Position(startLine, startColumn));
                if (Match('=')) return new Token(TokenType.Sym, "|=", new Position(startLine, startColumn));

                ErrorHandler.CompileError(Path, Source, "Unexpected '|'. Expected '||' or '|='.",
                    new Position(startLine, startColumn));
                break;
            }

            case '=':
            {
                Advance();
                if (Match('>')) return new Token(TokenType.Sym, "=>", new Position(startLine, startColumn));
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
                ErrorHandler.CompileError(Path, Source, $"Unknown operator or symbol '{c}' (U+{(int)c:X4}).",
                    new Position(startLine, startColumn));
                break;
            }
        }

        return null;
    }

    private bool Match(char expected)
    {
        if (_indx >= Source.Length || Source[_indx] != expected)
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

        while (_indx < Source.Length)
        {
            var c = Source[_indx];
            if (c == '\\')
            {
                Advance();
                if (_indx >= Source.Length) break;
                var next = Source[_indx];
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