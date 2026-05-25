namespace zscript;

public class ZsArithmeticError(string aType, string bType, string op)
    : Exception($"invalid operand types {aType}, {bType} for operator ({op})");