namespace obiwan;

public class Symbol(string name, int offset, bool constant, Position position)
{
    public string Name { get; } = name;
    public int Offset { get; } = offset;
    public bool Constant { get; } = constant;
    public Position Position { get; } = position;
}