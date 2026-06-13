using System.Globalization;
using System.Text;

namespace obiwan;

public struct LexerState
{
    public int Indx;
    public int Line;
    public int Colm;
}

public class Lexer(string path, string source)
{
    protected readonly string Path = path;
    protected readonly string Source = source;
    private int _colm = 1;
    private int _indx;
    private int _line = 1;

    protected LexerState SaveState()
    {
        return new LexerState { Indx = _indx, Line = _line, Colm = _colm };
    }

    protected void RestoreState(LexerState state)
    {
        _indx = state.Indx;
        _line = state.Line;
        _colm = state.Colm;
    }

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

            if (IsIdentifierStart(rune)) return ReadIdentifierOrKeyword();

            if (Rune.IsDigit(rune)) return ReadNumber();

            if (rune.Value == '"') return ReadString();

            if (rune.Value == '/' && _indx + 1 < Source.Length)
            {
                var nextRune = Rune.GetRuneAt(Source, _indx + rune.Utf16SequenceLength);

                if (nextRune.Value == '/')
                {
                    SkipLineComment();
                    continue;
                }

                if (nextRune.Value == '*')
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

    private static bool IsIdentifierStart(Rune rune)
    {
        return Rune.IsLetter(rune) || rune.Value == '_';
    }

    private static bool IsIdentifierContinue(Rune rune)
    {
        return Rune.IsLetterOrDigit(rune) || rune.Value == '_'
                                          || Rune.GetUnicodeCategory(rune) is
                                              UnicodeCategory.NonSpacingMark or
                                              UnicodeCategory.SpacingCombiningMark or
                                              UnicodeCategory.ConnectorPunctuation or
                                              UnicodeCategory.Format;
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

    private Rune PeekRune(int offset = 0)
    {
        var idx = _indx;
        for (var i = 0; i < offset && idx < Source.Length; i++)
            idx += Rune.GetRuneAt(Source, idx).Utf16SequenceLength;

        return idx < Source.Length ? Rune.GetRuneAt(Source, idx) : default;
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
            if (!IsIdentifierContinue(rune)) break;
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
        var rune = Rune.GetRuneAt(Source, _indx);

        if (rune.Value > 0x7F)
        {
            ErrorHandler.CompileError(Path, Source,
                $"Unknown operator or symbol '{rune}' (U+{rune.Value:X4}).",
                new Position(startLine, startColumn));
            Advance();
            return null;
        }

        var c = (char)rune.Value;

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
            case '~':
            {
                Advance();
                return new Token(TokenType.Sym, "~", new Position(startLine, startColumn));
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
                Advance();
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
        _colm++;
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
            var rune = Rune.GetRuneAt(Source, _indx);
            if (rune.Value == '\\')
            {
                Advance();
                if (_indx >= Source.Length) break;
                var nextRune = Rune.GetRuneAt(Source, _indx);
                switch (nextRune.Value)
                {
                    case '"': str.Append('"'); break;
                    case '\\': str.Append('\\'); break;
                    case 'n': str.Append('\n'); break;
                    case 'r': str.Append('\r'); break;
                    case 't': str.Append('\t'); break;
                    case '0': str.Append('\0'); break;
                    case 'u':
                    {
                        Advance();
                        var hexStart = _indx;
                        var hexCount = 0;
                        while (_indx < Source.Length && hexCount < 6 && IsHexDigit(Source[_indx]))
                        {
                            Advance();
                            hexCount++;
                        }

                        if (hexCount == 0)
                        {
                            ErrorHandler.CompileError(Path, Source,
                                "Invalid unicode escape sequence. Expected hex digits after '\\u'.",
                                new Position(startLine, startColumn));
                            break;
                        }

                        var hex = Source[hexStart.._indx];
                        var codepoint = Convert.ToInt32(hex, 16);
                        str.Append(char.ConvertFromUtf32(codepoint));
                        continue;
                    }
                    default: str.Append(nextRune.ToString()); break;
                }

                Advance();
            }
            else if (rune.Value == '"')
            {
                Advance();
                break;
            }
            else
            {
                str.Append(rune.ToString());
                Advance();
            }
        }

        return new Token(TokenType.Str, str.ToString(), new Position(startLine, startColumn));
    }
}