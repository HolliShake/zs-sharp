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
            B = member
        };
    }

    public static Ast CreateAwaitNode(Ast future, Position position)
    {
        return new Ast(AstType.AstAwait, position)
        {
            A = future
        };
    }

    public static Ast CreateUnaryNode(AstType astType, Ast future, Position position)
    {
        return new Ast(astType, position)
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

    public static Ast CreateTryCatchNode(Ast? tryHead, Ast? catchHead, Ast catchReceiver, Position position)
    {
        return new Ast(AstType.AstTryCatch, position)
        {
            A = tryHead,
            B = catchHead,
            C = catchReceiver
        };
    }

    public static Ast CreateIfNode(Ast condition, Ast thenBranch, Ast? elseBranch, Position position)
    {
        return new Ast(AstType.AstIf, position)
        {
            A = condition,
            B = thenBranch,
            C = elseBranch
        };
    }

    public static Ast CreateSwitchNode(Ast condition, Ast? caseHead, Ast? defaultValue, Position position)
    {
        return new Ast(AstType.AstSwitch, position)
        {
            A = condition,
            B = caseHead,
            C = defaultValue
        };
    }

    public static Ast CreateCaseNode(Ast condition, Ast value, Position position)
    {
        return new Ast(AstType.AstCase, position)
        {
            A = condition,
            B = value
        };
    }

    public static Ast CreateBlockNode(Ast? bodyHead, Position position)
    {
        return new Ast(AstType.AstBlock, position)
        {
            A = bodyHead
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

    public static Ast CreateExpressionStatementNode(Ast expression, Position position)
    {
        return new Ast(AstType.AstExpressionStatement, position)
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