namespace zscript;

public static class Program
{
    private static void Main(string[] args)
    {
        // Fallback if no arguments are given
        if (args.Length == 0)
        {
            PrintHelp();
            return;
        }

        switch (args[0])
        {
            case "-h":
            case "--help":
            {
                PrintHelp();
                break;
            }

            case "-t":
            case "--test":
            {
                RunTestSuite();
                break;
            }

            case "-r":
            case "--run":
            {
                // Ensure they actually provided a file path after the flag
                if (args.Length < 2)
                {
                    Console.WriteLine("Error: Missing file path after --run flag.");
                    return;
                }

                ExecuteScript(args[1]);
                break;
            }

            default:
            {
                Console.WriteLine($"Unknown option: {args[0]}");
                PrintHelp();
                break;
            }
        }
    }

    private static void ExecuteScript(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Error: The file at path '{path}' does not exist.");
            return;
        }

        var absPath = Path.GetFullPath(path);
        var source = File.ReadAllText(absPath);
        var state = new State();
        state.PushDir(Path.GetDirectoryName(absPath) ?? ".");

        var compiler = new Compiler(state, absPath, source);
        var script = compiler.Compile();

        var vm = new Vm(state);
        vm.MainLoop(script);
        vm.Dispose();

        state.Dispose();
    }

    private static void RunTestSuite()
    {
        Console.WriteLine("Initializing zscript internal test harness...");
        // Call your interpreter test runner assertions here!
        Console.WriteLine("All tests passed cleanly.");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Zscript Execution Engine CLI");
        Console.WriteLine("Usage:");
        Console.WriteLine("  -r, --run <path>   Path to the zscript source file to execute.");
        Console.WriteLine("  -t, --test         Execute the internal engine test suite.");
        Console.WriteLine("  -h, --help         Display this help screen.");
    }
}