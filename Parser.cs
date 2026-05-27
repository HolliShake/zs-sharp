using System.Diagnostics;

namespace zscript;

public class Parser(string path, string source) : Lexer(path, source)
{
    private Token? Lookahead { get; set; }

    private bool Check(TokenType type)
    {
        return Lookahead != null && Lookahead.Type == type;
    }

    private bool Check(string value)
    {
        return (Check(TokenType.Idn) || Check(TokenType.Key) || Check(TokenType.Sym)) &&
               Lookahead != null && Lookahead.Value == value;
    }

    private void Expect(TokenType type)
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        if (!Check(type))
            ErrorHandler.CompileError(
                Path, Source,
                $"Expected token of type {type}, but got {Lookahead} at position {Lookahead.Position}.",
                Lookahead.Position);

        Lookahead = Next();
    }

    private void Expect(string value)
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        if (!Check(value))
            ErrorHandler.CompileError(
                Path, Source,
                $"Expected token with value '{value}', but got {Lookahead} at position {Lookahead.Position}.",
                Lookahead.Position);

        Lookahead = Next();
    }

    private Ast? Terminal()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        switch (Lookahead.Type)
        {
            case TokenType.Idn:
            {
                var idnNode = Ast.CreateTerminalNode(AstType.AstName, Lookahead.Value, Lookahead.Position);
                Expect(TokenType.Idn);
                return idnNode;
            }
            case TokenType.Int:
            {
                var intNode = Ast.CreateTerminalNode(AstType.AstInt, Lookahead.Value, Lookahead.Position);
                Expect(TokenType.Int);
                return intNode;
            }
            case TokenType.Num:
            {
                var numNode = Ast.CreateTerminalNode(AstType.AstNumber, Lookahead.Value, Lookahead.Position);
                Expect(TokenType.Num);
                return numNode;
            }
            case TokenType.Str:
            {
                var strNode = Ast.CreateTerminalNode(AstType.AstString, Lookahead.Value, Lookahead.Position);
                Expect(TokenType.Str);
                return strNode;
            }
            default:
                return null;
        }
    }

    private Ast? Group()
    {
        if (Check("fn"))
            return FunctionExpression();
        if (Check("switch"))
            return Switch();

        return Terminal();
    }

    private Ast FunctionExpression()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("fn");
        Expect("(");
        var argc = 0;
        var parameterHead = Terminal();
        var parameterTail = parameterHead;
        if (parameterTail is not null and { Type: AstType.AstName })
        {
            argc++;
            while (Check(","))
            {
                Expect(",");
                var next = Terminal();
                parameterTail.Next = next;
                parameterTail = next;
                argc++;

                if (parameterTail == null)
                    ErrorHandler.CompileError(Path, Source, "expects parameter name after comma", Lookahead.Position);
                if (parameterTail is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "expects parameter name", parameterTail!.Position);
            }
        }

        Expect(")");

        var asynchronous = Check("async");

        if (asynchronous) Expect("async");

        Expect("{");
        var bodyHead = Statement();
        var bodyTail = bodyHead;
        while (bodyTail != null)
        {
            var next = Statement();
            bodyTail.Next = next;
            bodyTail = next;
        }

        Expect("}");

        return Ast.CreateFunctionNode(
            null!, parameterHead, bodyHead, argc, asynchronous, position
        );
    }

    private Ast Switch()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;

        Expect("switch");
        Expect("(");

        var condition = Expression();
        if (condition == null)
            ErrorHandler.CompileError(Path, Source, "Expected switch condition expression.", Lookahead.Position);

        Expect(")");
        Expect("{");

        Ast? caseHead = null;
        Ast? caseTail = null;
        Ast? defaultCase = null;
        var hasDefault = false;

        // Process all cases
        while (!Check("}"))
        {
            if (hasDefault)
                ErrorHandler.CompileError(Path, Source,
                    "The fallback/default case '_' must be the last branch in the switch statement.",
                    Lookahead.Position);

            var caseCondition = Expression();
            if (caseCondition == null)
                ErrorHandler.CompileError(Path, Source, "Expected case condition expression.", Lookahead.Position);

            Expect("=>");

            var caseValue = Expression();
            if (caseValue == null)
                ErrorHandler.CompileError(Path, Source, "Expected case value expression.", Lookahead.Position);

            if (caseCondition!.Type == AstType.AstName && caseCondition.Value == "_") hasDefault = true;

            var newCaseNode = Ast.CreateCaseNode(caseCondition, caseValue!, caseCondition.Position);

            if (caseHead == null)
            {
                caseHead = newCaseNode;
                caseTail = newCaseNode;
            }
            else
            {
                caseTail!.Next = newCaseNode;
                caseTail = newCaseNode;
            }

            if (hasDefault && defaultCase == null) defaultCase = newCaseNode;

            if (Check(","))
                Expect(",");
            else if (!Check("}"))
                ErrorHandler.CompileError(Path, Source, "Expected ',' or '}' after case arm.", Lookahead.Position);
        }

        if (defaultCase == null)
            ErrorHandler.CompileError(Path, Source, "Expected a default case in the switch statement.",
                Lookahead.Position);

        Expect("}");

        return Ast.CreateSwitchNode(condition!, caseHead!, defaultCase!, position);
    }

    private Ast? MemberOrCall()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var node = Group();
        if (node == null) return node;

        while (Check("->") || Check("("))
            if (Check("->"))
            {
                Expect("->");
                var member = Terminal();
                if (member == null) ErrorHandler.CompileError(Path, Source, "expects member", Lookahead.Position);

                if (member!.Type != AstType.AstName)
                    ErrorHandler.CompileError(Path, Source, "a member must be a valid identifier", Lookahead.Position);

                node = Ast.CreateMemberAccessNode(
                    node, member, node.Position
                );
            }
            else if (Check("("))
            {
                Expect("(");
                var argumentHead = Expression();
                var argumentTail = argumentHead;
                if (argumentTail is not null)
                    while (argumentTail != null && Check(","))
                    {
                        Expect(",");
                        var next = Expression();
                        argumentTail.Next = next;
                        argumentTail = next;

                        if (argumentTail is null)
                            ErrorHandler.CompileError(Path, Source, "expects argument after comma", Lookahead.Position);
                    }

                Expect(")");

                node = Ast.CreateFunctionCallNode(node, argumentHead, node.Position);
            }

        return node;
    }

    private Ast? Unary()
    {
        Debug.Assert(Lookahead != null, "Lookahead != null");
        var position = Lookahead.Position;
        if (Check("await"))
        {
            Expect("await");
            var futureNode = Unary();
            if (futureNode == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateAwaitNode(futureNode!, position);
        }

        return MemberOrCall();
    }

    private Ast? Multiplicative()
    {
        var lhs = Unary();

        if (lhs == null) return null;

        while (Check("*") || Check("/") || Check("%"))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Unary()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "*" => AstType.AstBinMul,
                "/" => AstType.AstBinDiv,
                "%" => AstType.AstBinMod,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Additive()
    {
        var lhs = Multiplicative();

        if (lhs == null) return null;

        while (Check("+") || Check("-"))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Multiplicative()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "+" => AstType.AstBinAdd,
                "-" => AstType.AstBinSub,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Shift()
    {
        var lhs = Additive();

        if (lhs == null) return null;

        while (Check("<<") || Check(">>"))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Additive()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "<<" => AstType.AstBinLShift,
                ">>" => AstType.AstBinRShift,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Relational()
    {
        var lhs = Shift();

        if (lhs == null) return null;

        while (Check("<") || Check("<=") || Check(">") || Check(">="))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Shift()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "<" => AstType.AstBinLt,
                "<=" => AstType.AstBinLe,
                ">" => AstType.AstBinGt,
                ">=" => AstType.AstBinGe,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Equality()
    {
        var lhs = Relational();

        if (lhs == null) return null;

        while (Check("==") || Check("!="))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Relational()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "==" => AstType.AstBinEq,
                "!=" => AstType.AstBinNe,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Bitwise()
    {
        var lhs = Equality();

        if (lhs == null) return null;

        while (Check("&") || Check("|") || Check("^"))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Equality()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "&" => AstType.AstBinAnd,
                "|" => AstType.AstBinOr,
                "^" => AstType.AstBinXor,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Logical()
    {
        var lhs = Bitwise();

        if (lhs == null) return null;

        while (Check("&&") || Check("||"))
        {
            Debug.Assert(Lookahead != null, "Lookahead is null");
            var opt = Lookahead.Value;
            Expect(TokenType.Sym);

            var rhs = Bitwise()
                      ?? throw new Exception($"Expected a terminal after '{opt}' at position {Lookahead.Position}.");

            lhs = Ast.CreateBinaryOperationNode(opt switch
            {
                "&&" => AstType.AstAnd,
                "||" => AstType.AstOr,
                _ => throw new Exception($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
            }, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Expression(bool nullable = true)
    {
        var node = Logical();
        if (node != null) return node;

        if (nullable) return null;

        Debug.Assert(Lookahead != null, "Lookahead is null");
        ErrorHandler.CompileError(Path, Source, "an expression is required.", Lookahead.Position);
        return null;
    }

    private Ast? Statement()
    {
        if (Check("fn")) return Function();
        if (Check("try")) return TryCatch();
        if (Check("if")) return If();
        if (Check("{")) return Block();
        if (Check("print")) return Print();
        if (Check("return")) return Return();
        return ExpressionStatement();
    }

    private Ast Function()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("fn");
        var func = Terminal();
        if (func == null) ErrorHandler.CompileError(Path, Source, "expects function name", Lookahead.Position);
        if (func is not { Type: AstType.AstName })
            ErrorHandler.CompileError(Path, Source, "expects function name", func!.Position);

        Expect("(");
        var argc = 0;
        var parameterHead = Terminal();
        var parameterTail = parameterHead;
        if (parameterTail is not null and { Type: AstType.AstName })
        {
            argc++;
            while (Check(","))
            {
                Expect(",");
                var next = Terminal();
                parameterTail.Next = next;
                parameterTail = next;
                argc++;

                if (parameterTail == null)
                    ErrorHandler.CompileError(Path, Source, "expects parameter name after comma", Lookahead.Position);
                if (parameterTail is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "expects parameter name", parameterTail!.Position);
            }
        }

        Expect(")");

        var asynchronous = Check("async");

        if (asynchronous) Expect("async");

        Expect("{");
        var bodyHead = Statement();
        var bodyTail = bodyHead;
        while (bodyTail != null)
        {
            var next = Statement();
            bodyTail.Next = next;
            bodyTail = next;
        }

        Expect("}");

        return Ast.CreateFunctionNode(
            func, parameterHead, bodyHead, argc, asynchronous, position
        );
    }

    private Ast TryCatch()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("try");
        Expect("{");
        var tryHead = Statement();
        var tryTail = tryHead;
        while (tryTail != null)
        {
            var next = Statement();
            tryTail.Next = next;
            tryTail = next;
        }

        Expect("}");
        Expect("catch");
        Expect("(");
        var errorVar = Terminal();
        if (errorVar == null)
            ErrorHandler.CompileError(Path, Source, "expects a catch receiver variable name", Lookahead.Position);
        if (errorVar is not { Type: AstType.AstName })
            ErrorHandler.CompileError(Path, Source, "expects a catch receiver variable name", errorVar!.Position);
        Expect(")");
        Expect("{");
        var catchHead = Statement();
        var catchTail = catchHead;
        while (catchTail != null)
        {
            var next = Statement();
            catchTail.Next = next;
            catchTail = next;
        }

        Expect("}");
        return Ast.CreateTryCatchNode(tryHead, catchHead, errorVar, position);
    }

    private Ast If()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("if");
        Expect("(");
        var condition = Expression();
        if (condition == null) ErrorHandler.CompileError(Path, Source, "expects condition", Lookahead.Position);
        Expect(")");
        var thenBranch = Statement();
        if (thenBranch == null) ErrorHandler.CompileError(Path, Source, "expects then branch", Lookahead.Position);
        Ast? elseBranch = null;
        if (!Check("else"))
            return Ast.CreateIfNode(
                condition!, thenBranch!, elseBranch, position
            );

        Expect("else");
        elseBranch = Statement();
        if (elseBranch == null) ErrorHandler.CompileError(Path, Source, "expects else branch", Lookahead.Position);

        return Ast.CreateIfNode(
            condition!, thenBranch!, elseBranch, position
        );
    }

    private Ast Block()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("{");
        var bodyHead = Statement();
        var bodyTail = bodyHead;
        while (bodyTail != null)
        {
            var next = Statement();
            bodyTail.Next = next;
            bodyTail = next;
        }

        Expect("}");
        return Ast.CreateBlockNode(bodyHead, position);
    }

    private Ast Print()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("print");
        var argumentHead = Expression(false);
        var argumentTail = argumentHead;
        if (argumentTail is not null)
            while (argumentTail != null && Check(","))
            {
                Expect(",");
                var next = Expression();
                argumentTail.Next = next;
                argumentTail = next;

                if (argumentTail is null)
                    ErrorHandler.CompileError(Path, Source, "expects argument after comma", Lookahead.Position);
            }

        Expect(";");
        return Ast.CreatePrintNode(argumentHead!, position);
    }

    private Ast Return()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("return");
        var expr = Expression();
        Expect(";");
        return Ast.CreateReturnNode(expr, position);
    }

    private Ast? ExpressionStatement()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        var expr = Expression();
        if (expr == null) return null;
        Expect(";");
        return Ast.CreateExpressionStatementNode(expr, position);
    }

    private Ast Program()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        var bodyHead = Statement();
        var bodyTail = bodyHead;
        while (bodyTail != null)
        {
            var next = Statement();
            bodyTail.Next = next;
            bodyTail = next;
        }

        Expect(TokenType.Eof);
        return Ast.CreateProgramNode(bodyHead, position);
    }

    protected Ast Parse()
    {
        Lookahead = Next();
        return Program();
    }
}