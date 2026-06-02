namespace zscript;

public interface IBuiltin
{
    public static abstract bool HasMethod(string methodName);

    public static abstract Func<Vm, ZsValue[], ZsValue> GetMethod(string methodName);
}