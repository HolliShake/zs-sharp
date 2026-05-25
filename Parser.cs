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
                path, source,
                $"Expected token of type {type}, but got {Lookahead} at position {Lookahead.Position}.",
                Lookahead.Position);

        Lookahead = Next();
    }

    private void Expect(string value)
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        if (!Check(value))
            ErrorHandler.CompileError(
                path, source,
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

    private Ast? MemberOrCall()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var node = Terminal();
        if (node == null) return node;

        while (Check("->") || Check("("))
            if (Check("->"))
            {
                Expect("->");
                var member = Terminal();
                if (member == null) ErrorHandler.CompileError(path, source, "expects member", Lookahead.Position);

                if (member!.Type != AstType.AstName)
                    ErrorHandler.CompileError(path, source, "a member must be a valid identifier", Lookahead.Position);

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
                            ErrorHandler.CompileError(path, source, "expects argument after comma", Lookahead.Position);
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
                ErrorHandler.CompileError(path, source, "an expression is expected", Lookahead.Position);

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
        ErrorHandler.CompileError(path, source, "an expression is required.", Lookahead.Position);
        return null;
    }

    private Ast? Statement()
    {
        if (Check("fn")) return Function();
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
        if (func == null) ErrorHandler.CompileError(path, source, "expects function name", Lookahead.Position);

        Expect("(");
        var argc = 0;
        var parameterHead = Terminal();
        var parameterTail = parameterHead;
        if (parameterTail is not null and { Type: AstType.AstName })
        {
            argc++;
            while (parameterTail != null && Check(","))
            {
                Expect(",");
                var next = Terminal();
                parameterTail.Next = next;
                parameterTail = next;
                argc++;

                if (parameterTail is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(path, source, "expects parameter name", Lookahead.Position);
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
            func!, parameterHead, bodyHead, argc, asynchronous, position
        );
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
                    ErrorHandler.CompileError(path, source, "expects argument after comma", Lookahead.Position);
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