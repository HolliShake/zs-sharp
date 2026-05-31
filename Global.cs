using System.Text;

namespace zscript;

public class Global
{
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