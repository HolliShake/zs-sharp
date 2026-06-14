using System.Diagnostics;

namespace obiwan;

public class Parser(string path, string source) : Lexer(path, source)
{
    private Token? Lookahead { get; set; }

    private bool Check(TokenType type)
    {
        return Lookahead != null && Lookahead.Type == type;
    }

    private bool Check(string value)
    {
        return (Check(TokenType.Key) || Check(TokenType.Sym)) &&
               Lookahead != null && Lookahead.Value == value;
    }

    private void Expect(TokenType type)
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        if (!Check(type))
            ErrorHandler.CompileError(
                Path, Source,
                $"Expected token of type {type}, but got {Lookahead.Value} at position {Lookahead.Position.Line}.",
                Lookahead.Position);

        Lookahead = Next();
    }

    private void Expect(string value)
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        if (!Check(value))
            ErrorHandler.CompileError(
                Path, Source,
                $"Expected token with value '{value}', but got {Lookahead.Value} at position {Lookahead.Position.Line}.",
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
            case TokenType.Key:
            {
                if (Check(Keyword.True) || Check(Keyword.False))
                {
                    var boolNode = Ast.CreateTerminalNode(AstType.AstBool, Lookahead.Value, Lookahead.Position);
                    Expect(TokenType.Key);
                    return boolNode;
                }

                if (Check(Keyword.Null))
                {
                    var nullNode = Ast.CreateTerminalNode(AstType.AstNull, Lookahead.Value, Lookahead.Position);
                    Expect(TokenType.Key);
                    return nullNode;
                }

                break;
            }
        }

        return null;
    }

    private Ast? Group()
    {
        if (Check(Keyword.Fn))
            return FunctionExpression();
        if (Check(Keyword.Switch))
            return SwitchExpression();
        if (Check("["))
            return ArrayLiteral();
        if (Check("{"))
            return ObjectLiteral();
        if (Check("("))
        {
            Expect("(");
            var node = Expression(false);
            Expect(")");
            return node;
        }

        return Terminal();
    }

    private Ast FunctionExpression()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Fn);
        Expect("(");
        var argc = 0;
        var parameterHead = Terminal();
        var parameterTail = parameterHead;
        if (parameterTail != null)
        {
            if (parameterTail is not { Type: AstType.AstName })
                ErrorHandler.CompileError(Path, Source, "expects parameter name", parameterTail.Position);

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
                    ErrorHandler.CompileError(Path, Source, "expects parameter name after comma",
                        parameterTail!.Position);
            }
        }

        Expect(")");

        var asynchronous = Check(Keyword.Async);

        if (asynchronous) Expect(Keyword.Async);

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

    private Ast SwitchExpression()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;

        Expect(Keyword.Switch);
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
        while (true)
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

            if (caseCondition is { Type: AstType.AstName, Value: "_" })
            {
                hasDefault = true;
                defaultCase = caseValue;
                goto ConsumeComma;
            }

            var newCaseNode = Ast.CreateCaseNode(caseCondition!, caseValue!, caseCondition!.Position);

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

            ConsumeComma: ;
            if (!Check(",")) break;
            Expect(",");
        }

        if (defaultCase == null)
            ErrorHandler.CompileError(Path, Source, "Expected a default case in the switch statement.",
                Lookahead.Position);

        Expect("}");

        return Ast.CreateSwitchNode(condition!, caseHead, defaultCase, position);
    }

    private Ast ArrayLiteral()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("[");
        var elementHead = ArrayElement();
        var elementTail = elementHead;

        if (elementTail != null)
            while (Check(","))
            {
                Expect(",");

                var next = ArrayElement();
                if (next == null)
                    ErrorHandler.CompileError(Path, Source, "expects expression after comma", Lookahead.Position);

                elementTail!.Next = next;
                elementTail = next;
            }

        Expect("]");

        return Ast.CreateArrayLiteralNode(elementHead!, position);
    }

    private Ast ObjectLiteral()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect("{");
        var elementHead = ObjectElement();
        var elementTail = elementHead;

        if (elementTail != null)
            while (Check(","))
            {
                Expect(",");

                var next = ObjectElement();
                if (next == null)
                    ErrorHandler.CompileError(Path, Source, "expects expression after comma", Lookahead.Position);

                elementTail!.Next = next;
                elementTail = next;
            }

        Expect("}");
        return Ast.CreateObjectLiteralNode(elementHead, position);
    }

    private Ast? ArrayElement()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        if (!Check("...")) return Expression();

        Expect("...");
        return Ast.CreateUnaryNode(AstType.AstSpread, Expression(false)!, position);
    }

    private Ast? ObjectElement()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        if (Check("..."))
        {
            Expect("...");
            return Ast.CreateUnaryNode(AstType.AstSpread, Expression(false)!, position);
        }

        var key = Terminal();
        if (key == null) return null;
        if (key is not { Type: AstType.AstName })
            ErrorHandler.CompileError(Path, Source, "expects identifier/name as key", key.Position);
        Expect(":");
        var val = Expression(false);
        return Ast.CreateKeyValuePairNode(key, val!, position);
    }

    private Ast? MemberOrCall()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var node = Group();
        if (node == null) return node;

        while (Check("->") || Check("[") || Check("("))
            if (Check("->"))
            {
                Expect("->");
                var member = Terminal();
                if (member == null) ErrorHandler.CompileError(Path, Source, "expects member", Lookahead.Position);
                if (member is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "a member must be a valid identifier/name",
                        member!.Position);

                node = Ast.CreateMemberAccessNode(
                    node, member, node.Position
                );
            }
            else if (Check("["))
            {
                Expect("[");
                var index = Expression(false);
                Expect("]");
                node = Ast.CreateIndexNode(node, index!, node.Position);
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

    private Ast? Postfix()
    {
        Debug.Assert(Lookahead != null, "Lookahead != null");
        var position = Lookahead.Position;
        var node = MemberOrCall();

        if (node == null) return null;

        if (!(Check("++") || Check("--"))) return node;

        var opt = Lookahead.Value;
        Expect(opt);

        return Ast.CreatePostfixNode(opt switch
        {
            "++" => AstType.AstPostPlusPlus,
            "--" => AstType.AstPostMinusMinus,
            _ => throw new InvalidSwitchValueException($"Unexpected operator '{opt}' at position {Lookahead.Position}.")
        }, node, position);
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

        if (Check("~"))
        {
            Expect("~");
            var operand = Unary();
            if (operand == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateUnaryNode(AstType.AstUnaBitNot, operand!, position);
        }

        if (Check("!"))
        {
            Expect("!");
            var operand = Unary();
            if (operand == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateUnaryNode(AstType.AstUnaNot, operand!, position);
        }

        if (Check("++"))
        {
            Expect("++");
            var operand = Unary();
            if (operand == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateUnaryNode(AstType.AstUnaPlusPlus, operand!, position);
        }

        if (Check("--"))
        {
            Expect("--");
            var operand = Unary();
            if (operand == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateUnaryNode(AstType.AstUnaMinusMinus, operand!, position);
        }

        if (Check("+"))
        {
            Expect("+");
            var operand = Unary();
            if (operand == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateUnaryNode(AstType.AstUnaPos, operand!, position);
        }

        if (Check("-"))
        {
            Expect("-");
            var operand = Unary();
            if (operand == null)
                ErrorHandler.CompileError(Path, Source, "an expression is expected", Lookahead.Position);

            return Ast.CreateUnaryNode(AstType.AstUnaMinus, operand!, position);
        }

        return Postfix();
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

        while (Check("==") || Check("!=") || Check("is") || Check("not"))
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
                "is" => AstType.AstBinIs,
                "not" => AstType.AstBinNot,
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

    private Ast? Assignment()
    {
        var lhs = Logical();
        if (lhs == null) return null;

        if (Check("=") || Check("+=") || Check("-=") ||
            Check("*=") || Check("/=") || Check("%=") ||
            Check("<<=") || Check(">>=") ||
            Check("&=") || Check("|=") || Check("^="))
        {
            var opt = Lookahead!.Value;
            Expect(TokenType.Sym);

            var rhs = Assignment() // right-recursive
                      ?? throw new Exception($"Expected expression after '{opt}' at position {Lookahead.Position}.");

            var astType = opt switch
            {
                "=" => AstType.AstAssign,
                "+=" => AstType.AstAddAssign,
                "-=" => AstType.AstSubAssign,
                "*=" => AstType.AstMulAssign,
                "/=" => AstType.AstDivAssign,
                "%=" => AstType.AstModAssign,
                "<<=" => AstType.AstLShiftAssign,
                ">>=" => AstType.AstRShiftAssign,
                "&=" => AstType.AstAndAssign,
                "|=" => AstType.AstOrAssign,
                "^=" => AstType.AstXorAssign,
                _ => throw new InvalidSwitchValueException($"Unexpected operator '{opt}'.")
            };

            return Ast.CreateBinaryOperationNode(astType, lhs, rhs, lhs.Position);
        }

        return lhs;
    }

    private Ast? Expression(bool nullable = true)
    {
        var node = Assignment();
        if (node != null) return node;

        if (nullable) return null;

        Debug.Assert(Lookahead != null, "Lookahead is null");
        ErrorHandler.CompileError(Path, Source, "an expression is required.", Lookahead.Position);
        return null;
    }

    private Ast? Statement()
    {
        if (Check(Keyword.Fn)) return Function();
        if (Check(Keyword.Var)) return VariableDeclaration(Keyword.Var);
        if (Check(Keyword.Local)) return VariableDeclaration(Keyword.Local);
        if (Check(Keyword.Const)) return VariableDeclaration(Keyword.Const);
        if (Check(Keyword.Try)) return TryCatch();
        if (Check(Keyword.From)) return FromForeach();
        if (Check(Keyword.Foreach)) return Foreach();
        if (Check(Keyword.While)) return While();
        if (Check(Keyword.If)) return If();
        if (Check(Keyword.Switch)) return Switch();
        if (Check("{")) return Block();
        if (Check(Keyword.Print)) return Print();
        if (Check(Keyword.Continue)) return Continue();
        if (Check(Keyword.Break)) return Break();
        if (Check(Keyword.Return)) return Return();
        return ExpressionStatement();
    }

    private Ast Function()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Fn);
        var func = Terminal();
        if (func == null) ErrorHandler.CompileError(Path, Source, "expects function name", Lookahead.Position);
        if (func is not { Type: AstType.AstName })
            ErrorHandler.CompileError(Path, Source, "expects function name", func!.Position);

        Expect("(");
        var argc = 0;
        var parameterHead = Terminal();
        var parameterTail = parameterHead;
        if (parameterTail != null)
        {
            if (parameterTail is not { Type: AstType.AstName })
                ErrorHandler.CompileError(Path, Source, "expects parameter name", parameterTail.Position);

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

        var asynchronous = Check(Keyword.Async);

        if (asynchronous) Expect(Keyword.Async);

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

    private Ast VariableDeclaration(string keyword)
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;

        var typeOfVariable = keyword switch
        {
            "var" => AstType.AstGlobalVar,
            "local" => AstType.AstLocalVar,
            "const" => AstType.AstConstVar,
            _ => throw new InvalidSwitchValueException($"keyword {keyword} not implemented")
        };

        Expect(keyword);

        if (Check("["))
        {
            var arrayPosition = Lookahead.Position;
            Expect("[");
            var variableNameHead = Terminal();
            var variableNameTail = variableNameHead;

            if (variableNameTail == null)
                ErrorHandler.CompileError(Path, Source, "expects variable name", Lookahead.Position);
            if (variableNameTail is not { Type: AstType.AstName })
                ErrorHandler.CompileError(Path, Source, "expects variable name", variableNameTail!.Position);

            while (Check(","))
            {
                Expect(",");
                var next = Terminal();

                if (next == null)
                    ErrorHandler.CompileError(Path, Source, "expects variable name after comma", arrayPosition);
                if (next is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "expects variable name after comma", arrayPosition);

                variableNameTail!.Next = next;
                variableNameTail = next;
            }

            Expect("]");
            if (!Check("="))
                ErrorHandler.CompileError(Path, Source, "missing initializer in destructuring declaration",
                    Lookahead.Position);
            Expect("=");
            var value = Expression();
            Expect(";");
            return Ast.CreateVariableNode(typeOfVariable,
                Ast.CreateInitializerNode(AstType.AstDestructureArrayInitializer, variableNameHead, value,
                    arrayPosition), position);
        }

        if (Check("{"))
        {
            var objectPosition = Lookahead.Position;
            Expect("{");
            var key = Terminal();
            if (key == null)
                ErrorHandler.CompileError(Path, Source, "expects property name", objectPosition);
            if (key is not { Type: AstType.AstName })
                ErrorHandler.CompileError(Path, Source, "expects property name", objectPosition);

            Expect(":");

            var alias = Terminal();
            if (alias == null)
                ErrorHandler.CompileError(Path, Source, "expects alias name", objectPosition);
            if (alias is not { Type: AstType.AstName })
                ErrorHandler.CompileError(Path, Source, "expects alias name", objectPosition);

            var keyValuePairHead = Ast.CreateKeyValuePairNode(key!, alias!, objectPosition);
            var keyValuePairTail = keyValuePairHead;

            while (Check(","))
            {
                Expect(",");
                key = Terminal();
                if (key == null)
                    ErrorHandler.CompileError(Path, Source, "expects property name after comma", objectPosition);
                if (key is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "expects property name after comma", objectPosition);

                Expect(":");

                alias = Terminal();
                if (alias == null)
                    ErrorHandler.CompileError(Path, Source, "expects alias name", objectPosition);
                if (alias is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "expects alias name", objectPosition);

                keyValuePairTail.Next = Ast.CreateKeyValuePairNode(key!, alias!, objectPosition);
                keyValuePairTail = keyValuePairTail.Next;
            }

            Expect("}");
            if (!Check("="))
                ErrorHandler.CompileError(Path, Source, "missing initializer in destructuring declaration",
                    Lookahead.Position);
            Expect("=");
            var value = Expression();
            Expect(";");

            return Ast.CreateVariableNode(typeOfVariable,
                Ast.CreateInitializerNode(AstType.AstDestructureObjectInitializer, keyValuePairHead, value,
                    objectPosition), position);
        }
        else
        {
            var pairPosition = Lookahead.Position;
            var variable = Terminal();
            if (variable == null)
                ErrorHandler.CompileError(Path, Source, "expects variable name", pairPosition);
            if (variable is not { Type: AstType.AstName })
                ErrorHandler.CompileError(Path, Source, "expects variable name", pairPosition);

            Ast? value = null;

            if (Check("="))
            {
                Expect("=");
                value = Expression(false);
            }

            var variableInit =
                Ast.CreateInitializerNode(AstType.AstVariableInitializer, variable!, value, pairPosition);
            var variableTail = variableInit;

            while (Check(","))
            {
                Expect(",");
                pairPosition = Lookahead.Position;

                variable = Terminal();
                if (variable == null)
                    ErrorHandler.CompileError(Path, Source, "expects variable name", pairPosition);
                if (variable is not { Type: AstType.AstName })
                    ErrorHandler.CompileError(Path, Source, "expects variable name", pairPosition);

                value = null;
                if (Check("="))
                {
                    Expect("=");
                    value = Expression(false);
                }

                variableTail.Next =
                    Ast.CreateInitializerNode(AstType.AstVariableInitializer, variable!, value, pairPosition);
                variableTail = variableTail.Next;
            }

            Expect(";");

            return Ast.CreateVariableNode(typeOfVariable, variableInit, position);
        }
    }

    private Ast Switch()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;

        Expect(Keyword.Switch);
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
        while (Check(Keyword.Case) || Check(Keyword.Default))
        {
            if (hasDefault)
                ErrorHandler.CompileError(Path, Source,
                    "The fallback/default case must be the last branch in the switch statement.",
                    Lookahead.Position);

            Ast? caseCondition = null;

            if (Check(Keyword.Default) && !hasDefault)
            {
                Expect(Keyword.Default);
                Expect(":");
                hasDefault = true;
            }
            else
            {
                Expect(Keyword.Case);
                caseCondition = Expression();
                var tailCaseCondition = caseCondition!;
                if (caseCondition == null)
                    ErrorHandler.CompileError(Path, Source, "Expected case condition expression.", Lookahead.Position);
                Expect(":");

                while (Check(Keyword.Case))
                {
                    Expect(Keyword.Case);
                    var nextCaseCondition = Expression();
                    if (nextCaseCondition == null)
                        ErrorHandler.CompileError(Path, Source, "Expected case condition expression.",
                            Lookahead.Position);
                    Expect(":");
                    tailCaseCondition.Next = nextCaseCondition!;
                    tailCaseCondition = tailCaseCondition.Next;
                }
            }

            var caseValue = Statement();
            if (caseValue == null)
                ErrorHandler.CompileError(Path, Source, "Expected case statement.", Lookahead.Position);

            if (caseCondition == null && hasDefault)
            {
                defaultCase = caseValue;
                continue;
            }

            var newCaseNode = Ast.CreateCaseNode(caseCondition!, caseValue!, position);

            if (caseHead == null)
                caseHead = newCaseNode;
            else
                caseTail!.Next = newCaseNode;

            caseTail = newCaseNode;
        }

        Expect("}");

        return Ast.CreateSwitchNode(condition!, caseHead, defaultCase, position);
    }

    private Ast TryCatch()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Try);
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
        Expect(Keyword.Catch);
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

    private Ast FromForeach()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.From);
        var start = Expression();
        if (start == null)
            ErrorHandler.CompileError(Path, Source, "Expected expression.", Lookahead.Position);
        Expect(Keyword.To);
        var end = Expression();
        if (end == null)
            ErrorHandler.CompileError(Path, Source, "Expected expression.", Lookahead.Position);
        Expect(Keyword.Foreach);
        var iterVar = Terminal();
        if (iterVar == null)
            ErrorHandler.CompileError(Path, Source, "expects a variable name", Lookahead.Position);
        if (iterVar is not { Type: AstType.AstName })
            ErrorHandler.CompileError(Path, Source, "expects a variable name", Lookahead.Position);
        Expect(",");
        var step = Expression();
        if (step == null)
            ErrorHandler.CompileError(Path, Source, "Expected expression.", Lookahead.Position);
        Expect(Keyword.Do);
        var thenBranch = Statement();
        if (thenBranch == null) ErrorHandler.CompileError(Path, Source, "expects statement", Lookahead.Position);
        return Ast.CreateFromForeachNode(
            start!, end!, step!, iterVar!, thenBranch!, position
        );
    }

    private Ast Foreach()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Foreach);
        var iterable = Expression();
        if (iterable == null)
            ErrorHandler.CompileError(Path, Source, "Expected expression.", Lookahead.Position);
        if (Check("{"))
        {
        }

        return null!;
    }

    private Ast While()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.While);
        var condition = Expression();
        if (condition == null) ErrorHandler.CompileError(Path, Source, "expects condition", Lookahead.Position);
        Expect(Keyword.Do);
        var thenBranch = Statement();
        if (thenBranch == null) ErrorHandler.CompileError(Path, Source, "expects statement", Lookahead.Position);
        return Ast.CreateWhileNode(
            condition!, thenBranch!, position
        );
    }

    private Ast If()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.If);
        var condition = Expression();
        if (condition == null) ErrorHandler.CompileError(Path, Source, "expects condition", Lookahead.Position);
        var thenBranch = Statement();
        if (thenBranch == null) ErrorHandler.CompileError(Path, Source, "expects then branch", Lookahead.Position);
        Ast? elseBranch = null;
        if (!Check(Keyword.Else))
            return Ast.CreateIfNode(
                condition!, thenBranch!, elseBranch, position
            );

        Expect(Keyword.Else);
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
        Expect(Keyword.Print);
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

    private Ast Continue()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Continue);
        Expect(";");
        return Ast.CreateContinueNode(position);
    }

    private Ast Break()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Break);
        Expect(";");
        return Ast.CreateBreakNode(position);
    }

    private Ast Return()
    {
        Debug.Assert(Lookahead != null, "Lookahead is null");
        var position = Lookahead.Position;
        Expect(Keyword.Return);
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