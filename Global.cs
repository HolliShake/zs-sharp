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
            vm.State.AutoLoader.InjectObject(moduleFrame);

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
}