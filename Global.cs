using System.Text;

namespace zscript;

public static class Global
{
    public static ZsValue Import(Vm vm, ZsValue[] args)
    {
        // 1. Validate arguments
        if (args.Length != 1)
            return ZsValue.FromErrorMessage(vm.Error, "import expects 1 argument", vm.BuildTracebackFromFrame());
        if (!ZsValue.IsInstanceOf(args[0], ValueType.String))
            return ZsValue.FromErrorMessage(vm.TypeError, "import expects a string", vm.BuildTracebackFromFrame());

        var moduleQualifiedPath = args[0].String();
        var cwd = vm.State.PeekDir();

        // 2. Resolve and normalize the exact file path
        var rawPath = Path.Combine(cwd, moduleQualifiedPath);
        var moduleFilePath = Path.GetFullPath(rawPath);

        // 3. Check if the file actually exists
        if (!File.Exists(moduleFilePath))
            return ZsValue.FromErrorMessage(vm.Error, $"Module not found: {moduleFilePath}",
                vm.BuildTracebackFromFrame());

        // 4. Cache & CIRCULAR IMPORT Check
        if (vm.State.LoadedModules.TryGetValue(moduleFilePath, out var cachedModule))
        {
            // If the module is in the cache but is 'null', it means we are currently inside its vm.Run() execution!
            if (cachedModule == null)
                return ZsValue.FromErrorMessage(
                    vm.Error,
                    $"Circular import detected. '{moduleQualifiedPath}' is already being loaded.",
                    vm.BuildTracebackFromFrame()
                );
            return cachedModule;
        }

        // 5. Get the directory
        var moduleDir = Path.GetDirectoryName(moduleFilePath);
        if (moduleDir == null)
            return ZsValue.FromErrorMessage(vm.Error, "Failed to get module directory", vm.BuildTracebackFromFrame());

        vm.State.PushDir(moduleDir);

        try
        {
            // 6. Read and Compile
            var sourceCode = File.ReadAllText(moduleFilePath);
            var compiler = new Compiler(vm.State, moduleFilePath, sourceCode);

            var moduleFrame = new Frame(vm.CurrentFrame, compiler.CompileAsModule(), true, false);
            vm.State.AutoLoader.InjectObject(vm, moduleFrame);

            // 7. PREVENT CIRCULAR IMPORT: Mark as 'loading' BEFORE execution
            // We inject a null (or a placeholder object) into the cache to lock the path.
            vm.State.LoadedModules[moduleFilePath] = null;

            // 8. Execute the module
            var moduleValue = vm.Run(moduleFrame);

            // 9. Update the cache with the actual finished module
            vm.State.LoadedModules[moduleFilePath] = moduleValue;

            return moduleValue;
        }
        catch (Exception ex)
        {
            return ZsValue.FromErrorMessage(vm.Error, $"Failed to import module: {ex.Message}",
                vm.BuildTracebackFromFrame());
        }
        finally
        {
            // 10. CRITICAL CLEANUP: If execution crashed, we must remove the 'loading' lock 
            // so the user can potentially try again (e.g., in a REPL environment).
            if (vm.State.LoadedModules.TryGetValue(moduleFilePath, out var val) && val == null)
                vm.State.LoadedModules.Remove(moduleFilePath);

            vm.State.PopDir();
        }
    }


    public static ZsValue Print(Vm vm, ZsValue[] args)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < args.Length; i++)
        {
            sb.Append(args[i]);
            if (i < args.Length - 1) sb.Append(' ');
        }

        Console.Write(sb.ToString());

        return vm.Null;
    }

    public static ZsValue Println(Vm vm, ZsValue[] args)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < args.Length; i++)
        {
            sb.Append(args[i]);
            if (i < args.Length - 1) sb.Append(' ');
        }

        Console.WriteLine(sb.ToString());

        return vm.Null;
    }

    public static ZsValue Scan(Vm vm, ZsValue[] args)
    {
        Print(vm, args);
        return ZsValue.FromString(Console.ReadLine() ?? "Something went wrong.");
    }

    public static ZsValue BuildMath(Vm instanceOfVm)
    {
        var mathClass = ZsValue.CreateZsClass(instanceOfVm.Object, "Math");

        // --- HELPER FUNCTIONS ---
        // These drastically reduce boilerplate while maintaining strict type checking
        // and ensuring AOT safety by using strongly typed delegates.

        ZsValue Wrap1Arg(Func<double, double> func, string name)
        {
            return ZsValue.FromNativeFunction((vm, args) =>
            {
                if (args.Length != 1)
                    return ZsValue.FromErrorMessage(vm.TypeError, $"{name} expects 1 argument",
                        vm.BuildTracebackFromFrame());
                if (!ZsValue.IsInstanceOf(args[0], ValueType.Number))
                    return ZsValue.FromErrorMessage(vm.TypeError, $"{name} expects a number",
                        vm.BuildTracebackFromFrame());

                return ZsValue.FromNumber(func(args[0].Number()));
            });
        }

        ZsValue Wrap2Args(Func<double, double, double> func, string name)
        {
            return ZsValue.FromNativeFunction((vm, args) =>
            {
                if (args.Length != 2)
                    return ZsValue.FromErrorMessage(vm.TypeError, $"{name} expects 2 arguments",
                        vm.BuildTracebackFromFrame());
                if (!ZsValue.IsInstanceOf(args[0], ValueType.Number) ||
                    !ZsValue.IsInstanceOf(args[1], ValueType.Number))
                    return ZsValue.FromErrorMessage(vm.TypeError, $"{name} expects numbers",
                        vm.BuildTracebackFromFrame());

                return ZsValue.FromNumber(func(args[0].Number(), args[1].Number()));
            });
        }

        ZsValue Wrap3Args(Func<double, double, double, double> func, string name)
        {
            return ZsValue.FromNativeFunction((vm, args) =>
            {
                if (args.Length != 3)
                    return ZsValue.FromErrorMessage(vm.TypeError, $"{name} expects 3 arguments",
                        vm.BuildTracebackFromFrame());
                if (!ZsValue.IsInstanceOf(args[0], ValueType.Number) ||
                    !ZsValue.IsInstanceOf(args[1], ValueType.Number) ||
                    !ZsValue.IsInstanceOf(args[2], ValueType.Number))
                    return ZsValue.FromErrorMessage(vm.TypeError, $"{name} expects numbers",
                        vm.BuildTracebackFromFrame());

                return ZsValue.FromNumber(func(args[0].Number(), args[1].Number(), args[2].Number()));
            });
        }

        // --- BUILD THE DICTIONARY ---
        var mathMethods = new Dictionary<string, ZsValue>
        {
            // Properties (Constants)
            ["PI"] = ZsValue.FromNumber(Math.PI),
            ["E"] = ZsValue.FromNumber(Math.E),
            ["Tau"] = ZsValue.FromNumber(Math.Tau), // Requires .NET 5+

            // 1-Argument Functions
            ["abs"] = Wrap1Arg(Math.Abs, "abs"),
            ["acos"] = Wrap1Arg(Math.Acos, "acos"),
            ["acosh"] = Wrap1Arg(Math.Acosh, "acosh"),
            ["asin"] = Wrap1Arg(Math.Asin, "asin"),
            ["asinh"] = Wrap1Arg(Math.Asinh, "asinh"),
            ["atan"] = Wrap1Arg(Math.Atan, "atan"),
            ["atanh"] = Wrap1Arg(Math.Atanh, "atanh"),
            ["cbrt"] = Wrap1Arg(Math.Cbrt, "cbrt"),
            ["ceil"] = Wrap1Arg(Math.Ceiling, "ceil"),
            ["cos"] = Wrap1Arg(Math.Cos, "cos"),
            ["cosh"] = Wrap1Arg(Math.Cosh, "cosh"),
            ["exp"] = Wrap1Arg(Math.Exp, "exp"),
            ["floor"] = Wrap1Arg(Math.Floor, "floor"),
            ["log"] = Wrap1Arg(Math.Log, "log"), // Base E
            ["log10"] = Wrap1Arg(Math.Log10, "log10"),
            ["log2"] = Wrap1Arg(Math.Log2, "log2"),
            ["round"] = Wrap1Arg(Math.Round, "round"),
            ["sin"] = Wrap1Arg(Math.Sin, "sin"),
            ["sinh"] = Wrap1Arg(Math.Sinh, "sinh"),
            ["sqrt"] = Wrap1Arg(Math.Sqrt, "sqrt"),
            ["tan"] = Wrap1Arg(Math.Tan, "tan"),
            ["tanh"] = Wrap1Arg(Math.Tanh, "tanh"),
            ["trunc"] = Wrap1Arg(Math.Truncate, "trunc"),

            // Special 1-Argument Function (Sign returns int, but we map to double for language consistency)
            ["sign"] = Wrap1Arg(x => Math.Sign(x), "sign"),

            // 2-Argument Functions
            ["atan2"] = Wrap2Args(Math.Atan2, "atan2"),
            ["max"] = Wrap2Args(Math.Max, "max"),
            ["min"] = Wrap2Args(Math.Min, "min"),
            ["pow"] = Wrap2Args(Math.Pow, "pow"),
            ["copySign"] = Wrap2Args(Math.CopySign, "copySign"),
            ["ieeeRemainder"] = Wrap2Args(Math.IEEERemainder, "ieeeRemainder"),

            // Overload for Log with a specified base: Math.Log(number, base)
            ["logBase"] = Wrap2Args(Math.Log, "logBase"),

            // Overload for Round with decimals: Math.Round(number, digits)
            ["roundTo"] = Wrap2Args((val, digits) => Math.Round(val, (int)digits), "roundTo"),

            // 3-Argument Functions
            ["clamp"] = Wrap3Args(Math.Clamp, "clamp")
        };

        var instance = ZsValue.CreateZsObject(ValueType.Object, mathClass, mathMethods);
        return instance;
    }
}