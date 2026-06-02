namespace obiwan;

public class Array : IBuiltin
{
    private static readonly string[] Methods = ["push", "length", "pop", "peek", "clear", "foreach"];

    public static bool HasMethod(string methodName)
    {
        return Methods.Contains(methodName);
    }

    public static Func<Vm, ObValue[], ObValue> GetMethod(string methodName)
    {
        return methodName switch
        {
            "push" => ArrayPushMethod,
            "pop" => ArrayPopMethod,
            "peek" => ArrayPeekMethod,
            "clear" => ArrayClearMethod,
            "foreach" => ArrayForeachMethod,
            "length" => ArrayLengthMethod,
            _ => throw new InvalidSwitchValueException($"method {methodName} not implemented")
        };
    }

    private static ObValue ArrayPushMethod(Vm vm, ObValue[] args)
    {
        if (args.Length < 2)
            return ObValue.FromErrorMessage(vm.ArgumentErrorClass, "push() expects at least 1 argument",
                vm.BuildTracebackFromFrame());
        args[0].Array().AddRange(args.Skip(1).Reverse());
        return vm.NullSingleton;
    }

    private static ObValue ArrayPopMethod(Vm vm, ObValue[] args)
    {
        var arr = args[0].Array();
        if (arr.Count == 0)
            return ObValue.FromErrorMessage(vm.IndexErrorClass, "pop() called on empty array", vm.BuildTracebackFromFrame());
        var item = arr[^1];
        arr.RemoveAt(arr.Count - 1);
        return item;
    }

    private static ObValue ArrayPeekMethod(Vm vm, ObValue[] args)
    {
        var arr = args[0].Array();
        return arr.Count != 0
            ? arr[^1]
            : ObValue.FromErrorMessage(vm.IndexErrorClass, "peek() called on empty array", vm.BuildTracebackFromFrame());
    }

    private static ObValue ArrayClearMethod(Vm vm, ObValue[] args)
    {
        args[0].Array().Clear();
        return vm.NullSingleton;
    }
    
    private static ObValue ArrayForeachMethod(Vm vm, ObValue[] args)
    {
        if (args.Length != 2) return ObValue.FromErrorMessage(vm.ArgumentErrorClass, "foreach() expects 1 argument(s)", vm.BuildTracebackFromFrame());
        var array = args[0].Array();
        for (var i = 0; i < array.Count; ++i)
        {
            vm.CurrentFrame!.PushOperand(array[i]);
            vm.CurrentFrame!.PushOperand(ObValue.FromInt(i));
            vm.CurrentFrame!.PushOperand(args[1]);
            var result = vm.DoCall(vm.CurrentFrame, 2);
            if (ObValue.IsInstanceOf(result, "Error")) return result;
        }
        
        return vm.NullSingleton;
    }

    private static ObValue ArrayLengthMethod(Vm vm, ObValue[] args)
    {
        return ObValue.FromInt(args[0].Array().Count);
    }
}