using System.Diagnostics;

namespace zscript;

public class Compiler(State state, string path, string source) : Parser(path, source)
{
    private void Expr(Code code, SymbolTable table, Ast node)
    {
        switch (node.Type)
        {
            case AstType.AstName:
            {
                var name = node.Value;
                if (!table.SymbolExists(name))
                    ErrorHandler.CompileError(Path, Source, "Symbol not found", node.Position);

                var lookupDetail = table.Find(name);

                if (!lookupDetail.IsLocal)
                {
                    // Register
                    var address = code.AllocateLocal();
                    code.AddCapture((lookupDetail.Depth, lookupDetail.Symbol.Offset, address));
                    // Console.WriteLine($"{lookupDetail.Depth}, {lookupDetail.Symbol.Offset}, {address}");
                    table.Add(name, address, false, node.Position);
                    code.Emit(OpCode.LoadCapture, address);
                    break;
                }

                code.Emit(OpCode.LoadLocal, lookupDetail.Symbol.Offset);
                break;
            }
            case AstType.AstInt:
            {
                var index = state.SaveInt(int.Parse(node.Value));
                code.Emit(OpCode.LoadConst, index);
                break;
            }
            case AstType.AstNumber:
            {
                var index = state.SaveNum(double.Parse(node.Value));
                code.Emit(OpCode.LoadConst, index);
                break;
            }
            case AstType.AstString:
            {
                var index = state.SaveStr(node.Value);
                code.Emit(OpCode.LoadConst, index);
                break;
            }
            case AstType.AstMemberAccess:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                code.Emit(OpCode.GetAttr, node.B.Value);
                break;
            }
            case AstType.AstFunctionCall:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                var callable = node.A;
                var argsHead = node.B;
                var argc = 0;

                if (callable.Type == AstType.AstMemberAccess) Expr(code, table, callable.A!);

                while (argsHead != null)
                {
                    Expr(code, table, argsHead);
                    argsHead = argsHead.Next;
                    ++argc;
                }

                var isMethodCall = callable.Type == AstType.AstMemberAccess;
                if (isMethodCall)
                {
                    code.Emit(OpCode.LoadString, callable.B!.Value);
                    code.Emit(OpCode.CallMethod, argc);
                }
                else
                {
                    Expr(code, table, callable);
                    code.Emit(OpCode.Call, argc);
                }

                break;
            }
            case AstType.AstAwait:
            {
                Debug.Assert(node is { A: not null }, "node.A is null");
                Expr(code, table, node.A);
                code.Emit(OpCode.Await);
                break;
            }
            case AstType.AstBinMul:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BinMul);
                break;
            }
            case AstType.AstBinDiv:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BinDiv);
                break;
            }
            case AstType.AstBinMod:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BinMod);
                break;
            }
            case AstType.AstBinAdd:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BinAdd);
                break;
            }
            case AstType.AstBinSub:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BinSub);
                break;
            }
            default:
            {
                ErrorHandler.CompileError(Path, Source, "Node not implemented", node.Position);
                break;
            }
        }
    }

    private void Stmt(Code code, SymbolTable table, Ast node)
    {
        switch (node.Type)
        {
            case AstType.AstFunction:
            {
                Function(code, table, node);
                break;
            }
            case AstType.AstPrint:
            {
                Print(code, table, node);
                break;
            }
            case AstType.AstReturn:
            {
                Return(code, table, node);
                break;
            }
            case AstType.AstExpressionStatement:
            {
                Expr(code, table, node.A!);
                code.Emit(OpCode.PopTop);
                break;
            }
            default:
            {
                ErrorHandler.CompileError(Path, Source, "Node not implemented", node.Position);
                break;
            }
        }
    }

    private void Print(Code code, SymbolTable table, Ast node)
    {
        Debug.Assert(node is { A: not null }, "node.A is null");
        var parameterHead = node.A;
        var count = 0;
        while (parameterHead != null)
        {
            Expr(code, table, parameterHead);
            parameterHead = parameterHead.Next;
            count++;
        }

        code.Emit(OpCode.Print, count);
    }

    private void Return(Code code, SymbolTable table, Ast node)
    {
        var expression = node.A;
        if (expression == null)
            code.Emit(OpCode.LoadNull);
        else
            Expr(code, table, expression);

        code.Emit(OpCode.Return);
    }

    private void Function(Code code, SymbolTable table, Ast node)
    {
        var fnCode = new Code(node.IntArg0, node.Flag0);
        var locals = new SymbolTable(ScopeType.Function, table);
        Debug.Assert(node is { A: not null, B: not null, C: not null }, "node.A or node.B or node.C is null");

        var functionAddress = code.AllocateLocal();
        table.Add(node.A.Value, functionAddress, false, node.Position);

        var paramHead = node.B;
        while (paramHead != null)
        {
            var name = paramHead.Value;
            if (locals.AlreadyExists(name))
                ErrorHandler.CompileError(Path, Source, "Parameter already exists", paramHead.Position);

            var paramAddress = fnCode.AllocateLocal();
            locals.Add(name, paramAddress, false, node.Position);
            fnCode.Emit(OpCode.StoreLocal, paramAddress);

            paramHead = paramHead.Next;
        }

        var bodyHead = node.C;
        while (bodyHead != null)
        {
            Stmt(fnCode, locals, bodyHead);
            bodyHead = bodyHead.Next;
        }

        fnCode.Emit(OpCode.LoadNull);
        fnCode.Emit(OpCode.Return);

        var addressOfCode = state.SaveCodeTemplate(fnCode);
        code.Emit(OpCode.LoadFunction, addressOfCode);
        code.Emit(OpCode.StoreName, functionAddress);
    }

    private ZsValue Program(Ast node)
    {
        var code = new Code(0, false);
        var globalTable = new SymbolTable(ScopeType.Global, null);
        var bodyHead = node.A;
        while (bodyHead != null)
        {
            Stmt(code, globalTable, bodyHead);
            bodyHead = bodyHead.Next;
        }

        code.Emit(OpCode.LoadNull);
        code.Emit(OpCode.Return);
        return ZsValue.FromCodeToScript(code);
    }

    public ZsValue Compile()
    {
        var ast = Parse();
        return Program(ast);
    }
}