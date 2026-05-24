namespace zscript;

public class Token(TokenType type, string value, Position position)
{
    public readonly Position Position = position;
    public readonly TokenType Type = type;
    public readonly string Value = value;

    public override string ToString()
    {
        return $"{Type}('{Value}') at {Position}";
    }
}