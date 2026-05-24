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
                    ErrorHandler.CompileError(path, source, "Symbol not found", node.Position);

                var lookupDetail = table.Find(name);

                if (!lookupDetail.IsLocal)
                {
                    // Register
                    var address = code.AllocateLocal();
                    code.AddCapture((lookupDetail.Depth, lookupDetail.Symbol.Offset, address));
                    // Console.WriteLine($"{lookupDetail.Depth}, {lookupDetail.Symbol.Offset}, {address}");
                    table.Add(name, address, false, node.Position);
                    code.Emit(OpCode.LOADCAPTURE, address);
                    break;
                }

                code.Emit(OpCode.LOADLOCAL, lookupDetail.Symbol.Offset);
                break;
            }
            case AstType.AstInt:
            {
                var index = state.SaveInt(int.Parse(node.Value));
                code.Emit(OpCode.LOADCONST, index);
                break;
            }
            case AstType.AstNumber:
            {
                var index = state.SaveNum(double.Parse(node.Value));
                code.Emit(OpCode.LOADCONST, index);
                break;
            }
            case AstType.AstString:
            {
                var index = state.SaveStr(node.Value);
                code.Emit(OpCode.LOADCONST, index);
                break;
            }
            case AstType.AstMemberAccess:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                code.Emit(OpCode.GETATTR, node.B.Value);
                break;
            }
            case AstType.AstFunctionCall:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                var callable = node.A;
                var argsHead = node.B;
                var argc = 0;
                
                if (callable.Type == AstType.AstMemberAccess)
                {
                    Expr(code, table, callable.A!);
                }
                
                while (argsHead != null)
                {
                    Expr(code, table, argsHead);
                    argsHead = argsHead.Next;
                    ++argc;
                }

                var isMethodCall = callable.Type == AstType.AstMemberAccess;
                if (isMethodCall)
                {
                    code.Emit(OpCode.LOADSTRING, callable.B!.Value);
                    code.Emit(OpCode.CALLMETHOD, argc);
                }
                else
                {
                    Expr(code, table, callable);
                    code.Emit(OpCode.CALL, argc);
                }
                break;
            }
            case AstType.AstAwait:
            {
                Debug.Assert(node is { A: not null }, "node.A is null");
                Expr(code, table, node.A);
                code.Emit(OpCode.AWAIT);
                break;
            }
            case AstType.AstBinMul:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BINMUL);
                break;
            }
            case AstType.AstBinDiv:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BINDIV);
                break;
            }
            case AstType.AstBinMod:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BINMOD);
                break;
            }
            case AstType.AstBinAdd:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BINADD);
                break;
            }
            case AstType.AstBinSub:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.Emit(OpCode.BINSUB);
                break;
            }
            default:
            {
                ErrorHandler.CompileError(path, source, "Node not implemented", node.Position);
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
            default:
            {
                Expr(code, table, node);
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

        code.Emit(OpCode.PRINT, count);
    }
    
    private void Return(Code code, SymbolTable table, Ast node)
    {
        var expression = node.A;
        if (expression == null)
        {
            code.Emit(OpCode.LOADNULL);
        }
        else
        {
            Expr(code, table, expression);
        }

        code.Emit(OpCode.RETURN);
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
                ErrorHandler.CompileError(path, source, "Parameter already exists", paramHead.Position);

            var paramAddress = fnCode.AllocateLocal();
            locals.Add(name, paramAddress, false, node.Position);
            fnCode.Emit(OpCode.STORELOCAL, paramAddress);

            paramHead = paramHead.Next;
        }

        var bodyHead = node.C;
        while (bodyHead != null)
        {
            Stmt(fnCode, locals, bodyHead);
            bodyHead = bodyHead.Next;
        }

        fnCode.Emit(OpCode.LOADNULL);
        fnCode.Emit(OpCode.RETURN);

        var addressOfCode = state.SaveCodeTemplate(fnCode);
        code.Emit(OpCode.LOADFUNCTION, addressOfCode);
        code.Emit(OpCode.STORENAME, functionAddress);
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

        code.Emit(OpCode.LOADNULL);
        code.Emit(OpCode.RETURN);
        return ZsValue.FromCodeToScript(code);
    }

    public ZsValue Compile()
    {
        var ast = Parse();
        return Program(ast);
    }
}