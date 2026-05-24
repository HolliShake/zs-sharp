namespace zscript;

public class ZsArithmeticError : Exception
{
    public ZsArithmeticError(string aType, string bType, string op) : base(
        $"invalid operand types {aType}, {bType} for operator ({op})")
    {
    }
}