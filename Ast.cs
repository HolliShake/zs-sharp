namespace zscript;

public class Ast
{
    public readonly Position Position;
    public readonly AstType Type;
    public Ast? A;
    public Ast? B;
    public Ast? C;
    public Ast? D;
    public bool Flag0;
    public bool Flag1;
    public int IntArg0;
    public int IntArg1;
    public Ast? Next;
    public string Value;

    private Ast(AstType type, Position position)
    {
        Type = type;
        Position = position;
        Value = string.Empty;
        A = null;
        B = null;
        C = null;
        D = null;
        Next = null;
        Flag0 = false;
        Flag1 = false;
        IntArg0 = 0;
        IntArg1 = 0;
    }

    public static Ast CreateTerminalNode(AstType type, string value, Position position)
    {
        return new Ast(type, position)
        {
            Value = value
        };
    }

    public static Ast CreateMemberAccessNode(Ast indexable, Ast member, Position position)
    {
        return new Ast(AstType.AstMemberAccess, position)
        {
            A = indexable,
            B = member,
        };
    }

    public static Ast CreateAwaitNode(Ast future, Position position)
    {
        return new Ast(AstType.AstAwait, position)
        {
            A = future
        };
    }

    public static Ast CreateFunctionCallNode(Ast callable, Ast? argumentHead, Position position)
    {
        return new Ast(AstType.AstFunctionCall, position)
        {
            A = callable,
            B = argumentHead
        };
    }

    public static Ast CreateBinaryOperationNode(AstType type, Ast left, Ast right, Position position)
    {
        return new Ast(type, position)
        {
            A = left,
            B = right
        };
    }

    public static Ast CreateFunctionNode(Ast name, Ast? parameterHead, Ast? bodyHead, int argCount, bool asynchronous,
        Position position)
    {
        return new Ast(AstType.AstFunction, position)
        {
            A = name,
            B = parameterHead,
            C = bodyHead,
            IntArg0 = argCount,
            Flag0 = asynchronous
        };
    }

    public static Ast CreatePrintNode(Ast parameterHead, Position position)
    {
        return new Ast(AstType.AstPrint, position)
        {
            A = parameterHead
        };
    }
    
    public static Ast CreateReturnNode(Ast? expression, Position position)
    {
        return new Ast(AstType.AstReturn, position)
        {
            A = expression
        };
    }

    public static Ast CreateProgramNode(Ast? bodyHead, Position position)
    {
        return new Ast(AstType.AstProgram, position)
        {
            A = bodyHead
        };
    }
}