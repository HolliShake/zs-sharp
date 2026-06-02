namespace zscript;

public class Array : IBuiltin
{
    private static readonly string[] Methods = ["push", "length", "pop", "peek", "clear"];

    public static bool HasMethod(string methodName)
    {
        return Methods.Contains(methodName);
    }

    public static Func<Vm, ZsValue[], ZsValue> GetMethod(string methodName)
    {
        return methodName switch
        {
            "push" => ArrayPushMethod,
            "pop" => ArrayPopMethod,
            "peek" => ArrayPeekMethod,
            "clear" => ArrayClearMethod,
            "length" => ArrayLengthMethod,
            _ => throw new InvalidSwitchValueException($"method {methodName} not implemented")
        };
    }

    private static ZsValue ArrayPushMethod(Vm vm, ZsValue[] args)
    {
        if (args.Length < 2)
            return ZsValue.FromErrorMessage(vm.ErrorClass, "push() expects at least 1 argument",
                vm.BuildTracebackFromFrame());
        args[0].Array().AddRange(args.Skip(1).Reverse());
        return vm.NullSingleton;
    }

    private static ZsValue ArrayPopMethod(Vm vm, ZsValue[] args)
    {
        var arr = args[0].Array();
        if (arr.Count == 0)
            return ZsValue.FromErrorMessage(vm.ErrorClass, "pop() called on empty array", vm.BuildTracebackFromFrame());
        var item = arr[^1];
        arr.RemoveAt(arr.Count - 1);
        return item;
    }

    private static ZsValue ArrayPeekMethod(Vm vm, ZsValue[] args)
    {
        var arr = args[0].Array();
        return arr.Count != 0
            ? arr[^1]
            : ZsValue.FromErrorMessage(vm.ErrorClass, "peek() called on empty array", vm.BuildTracebackFromFrame());
    }

    private static ZsValue ArrayClearMethod(Vm vm, ZsValue[] args)
    {
        args[0].Array().Clear();
        return vm.NullSingleton;
    }

    private static ZsValue ArrayLengthMethod(Vm vm, ZsValue[] args)
    {
        return ZsValue.FromInt(args[0].Array().Count);
    }
}