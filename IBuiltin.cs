namespace zscript;

public interface IBuiltin
{
    public static bool HasMethod(string methodName)
    {
        throw new NotImplementedException("Please override HasMethod");
    }

    public static Func<Vm, ZsValue[], ZsValue> GetMethod(string methodName)
    {
        throw new NotImplementedException("Please override GetMethod");
    }
}