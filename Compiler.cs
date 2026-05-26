using System.Diagnostics;

namespace zscript;

public class Compiler : Parser
{
    public Compiler(State state, string path, string source) : base(path, source)
    {
        State = state;
        ModuleId = State.RegisterModuleName(path);
    }

    private State State { get; }
    private int ModuleId { get; }

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

                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.LoadLocal, lookupDetail.Symbol.Offset);
                break;
            }
            case AstType.AstInt:
            {
                var index = State.SaveInt(int.Parse(node.Value));
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.LoadConst, index);
                break;
            }
            case AstType.AstNumber:
            {
                var index = State.SaveNum(double.Parse(node.Value));
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.LoadConst, index);
                break;
            }
            case AstType.AstString:
            {
                var index = State.SaveStr(node.Value);
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.LoadConst, index);
                break;
            }
            case AstType.AstMemberAccess:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                code.EmitLine(ModuleId, node.Position.Line);
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
                    code.EmitLine(ModuleId, callable.B!.Position.Line);
                    code.Emit(OpCode.LoadString, callable.B!.Value);
                    code.EmitLine(ModuleId, callable.B!.Position.Line);
                    code.Emit(OpCode.CallMethod, argc);
                }
                else
                {
                    Expr(code, table, callable);
                    code.EmitLine(ModuleId, node.Position.Line);
                    code.Emit(OpCode.Call, argc);
                }

                break;
            }
            case AstType.AstAwait:
            {
                Debug.Assert(node is { A: not null }, "node.A is null");
                Expr(code, table, node.A);
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.Await);
                break;
            }
            case AstType.AstBinMul:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.BinMul);
                break;
            }
            case AstType.AstBinDiv:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.BinDiv);
                break;
            }
            case AstType.AstBinMod:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.BinMod);
                break;
            }
            case AstType.AstBinAdd:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.EmitLine(ModuleId, node.Position.Line);
                code.Emit(OpCode.BinAdd);
                break;
            }
            case AstType.AstBinSub:
            {
                Debug.Assert(node is { A: not null, B: not null }, "node.A or node.B is null");
                Expr(code, table, node.A);
                Expr(code, table, node.B);
                code.EmitLine(ModuleId, node.Position.Line);
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
            case AstType.AstTryCatch:
            {
                TryCatch(code, table, node);
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

        code.EmitLine(ModuleId, node.Position.Line);
        code.Emit(OpCode.Print, count);
    }

    private void Return(Code code, SymbolTable table, Ast node)
    {
        var expression = node.A;
        if (expression == null)
        {
            code.EmitLine(ModuleId, node.Position.Line);
            code.Emit(OpCode.LoadNull);
        }
        else
        {
            Expr(code, table, expression);
        }

        code.Emit(OpCode.Return);
    }

    private void Function(Code code, SymbolTable table, Ast node)
    {
        Debug.Assert(node is { A: not null }, "node.A is null");
        var fnCode = new Code(node.A.Value, node.IntArg0, node.Flag0);
        var locals = new SymbolTable(ScopeType.Function, table);

        var functionAddress = code.AllocateLocal();
        table.Add(node.A.Value, functionAddress, false, node.Position);

        var position = node.Position;

        var paramHead = node.B;
        while (paramHead != null)
        {
            var name = paramHead.Value;
            if (locals.AlreadyExists(name))
                ErrorHandler.CompileError(Path, Source, "Parameter already exists", paramHead.Position);

            var paramAddress = fnCode.AllocateLocal();
            locals.Add(name, paramAddress, false, paramHead.Position);
            fnCode.EmitLine(ModuleId, paramHead.Position.Line);
            fnCode.Emit(OpCode.StoreLocal, paramAddress);
            position = paramHead.Position;
            paramHead = paramHead.Next;
        }

        var bodyHead = node.C;
        while (bodyHead != null)
        {
            Stmt(fnCode, locals, bodyHead);
            position = bodyHead.Position;
            bodyHead = bodyHead.Next;
        }

        fnCode.EmitLine(ModuleId, position.Line);
        fnCode.Emit(OpCode.LoadNull);
        fnCode.EmitLine(ModuleId, position.Line);
        fnCode.Emit(OpCode.Return);

        var addressOfCode = State.SaveCodeTemplate(fnCode);
        code.EmitLine(ModuleId, node.Position.Line);
        code.Emit(OpCode.LoadFunction, addressOfCode);
        code.EmitLine(ModuleId, node.Position.Line);
        code.Emit(OpCode.StoreName, functionAddress);
    }

    private void TryCatch(Code code, SymbolTable table, Ast node)
    {
        Debug.Assert(node is { C: not null }, "node.C is null");
        var position = node.Position;
        
        code.EmitLine(ModuleId, position.Line);
        var catchAddress = code.EmitJump(OpCode.SetupTry);
        
        var tryTable = new SymbolTable(ScopeType.TryBlock, table);
        
        var tryHead = node.A;
        while (tryHead != null)
        {
            Stmt(code, tryTable, tryHead);
            position = tryHead.Position;
            tryHead = tryHead.Next;
        }
        
        code.EmitLine(ModuleId, position.Line);
        var toEndTry = code.EmitJump(OpCode.Jump);

        var catchTable = new SymbolTable(ScopeType.CatchBlock, table);
        
        code.Label(catchAddress);
        
        var errorVar = node.C;
        var errorVarAddress = code.AllocateLocal();
        catchTable.Add(errorVar.Value, errorVarAddress, false, errorVar.Position);
        
        code.EmitLine(ModuleId, errorVar.Position.Line);
        code.Emit(OpCode.StoreLocal, errorVarAddress);
        
        var catchHead = node.B;
        while (catchHead != null)
        {
            Stmt(code, catchTable, catchHead);
            catchHead = catchHead.Next;
        }
        
        code.Label(toEndTry);
        
        // end catch, pop try
        code.EmitLine(ModuleId, position.Line);
        code.Emit(OpCode.PopTry);
    }

    private ZsValue Program(Ast node)
    {
        var code = new Code("main", 0, false);
        var globalTable = new SymbolTable(ScopeType.Global, null);

        var position = node.Position;

        var bodyHead = node.A;
        while (bodyHead != null)
        {
            Stmt(code, globalTable, bodyHead);
            position = bodyHead.Position;
            bodyHead = bodyHead.Next;
        }

        code.EmitLine(ModuleId, position.Line);
        code.Emit(OpCode.LoadNull);
        code.EmitLine(ModuleId, position.Line);
        code.Emit(OpCode.Return);

        return ZsValue.FromCodeToScript(code);
    }

    public ZsValue Compile()
    {
        var ast = Parse();
        return Program(ast);
    }
}