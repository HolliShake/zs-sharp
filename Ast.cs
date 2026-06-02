namespace obiwan;

public class Ast : IDisposable
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

    public void Dispose()
    {
        Flag0 = false;
        Flag1 = false;
        IntArg0 = 0;
        IntArg1 = 0;
        Value = string.Empty;
        A?.Dispose();
        B?.Dispose();
        C?.Dispose();
        D?.Dispose();
        Next?.Dispose();
        A = null;
        B = null;
        C = null;
        D = null;
        Next = null;
    }

    public static Ast CreateTerminalNode(AstType type, string value, Position position)
    {
        return new Ast(type, position)
        {
            Value = value
        };
    }

    public static Ast CreateArrayLiteralNode(Ast? elementHead, Position position)
    {
        return new Ast(AstType.AstArrayLiteral, position)
        {
            A = elementHead
        };
    }

    public static Ast CreateObjectLiteralNode(Ast? elementHead, Position position)
    {
        return new Ast(AstType.AstObjectLiteral, position)
        {
            A = elementHead
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

    public static Ast CreateIndexNode(Ast indexable, Ast index, Position position)
    {
        return new Ast(AstType.AstIndex, position)
        {
            A = indexable,
            B = index
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

    public static Ast CreateKeyValuePairNode(Ast key, Ast value, Position position)
    {
        return new Ast(AstType.AstKeyValuePair, position)
        {
            A = key,
            B = value
        };
    }

    public static Ast CreateInitializerNode(AstType type, Ast? variableOrDestructuring, Ast? value, Position position)
    {
        return new Ast(type, position)
        {
            A = variableOrDestructuring,
            B = value
        };
    }

    public static Ast CreateVariableNode(AstType type, Ast initializerHead, Position position)
    {
        return new Ast(type, position)
        {
            A = initializerHead
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

    public static Ast CreateWhileNode(Ast condition, Ast thenBranch, Position position)
    {
        return new Ast(AstType.AstWhile, position)
        {
            A = condition,
            B = thenBranch
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

    public static Ast CreateContinueNode(Position position)
    {
        return new Ast(AstType.AstContinue, position);
    }

    public static Ast CreateBreakNode(Position position)
    {
        return new Ast(AstType.AstBreak, position);
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