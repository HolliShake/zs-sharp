namespace zscript;

public class Array : IBuiltin
{
    private static readonly string[] Methods = ["length"];
    
    public static bool HasMethod(string methodName)
    {
        return  Methods.Contains(methodName);
    }

    public static Func<Vm, ZsValue[], ZsValue> GetMethod(string methodName)
    {
        return methodName switch
        {
            "length" => ArrayLengthMethod,
            _ => throw new NotImplementedException($"method {methodName} not implemented")
        };
    }

    private static ZsValue ArrayLengthMethod(Vm vm, ZsValue[] args)
    {
        return ZsValue.FromInt(args[0].Array().Count);
    }
}