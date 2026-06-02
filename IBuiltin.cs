namespace obiwan;

public interface IBuiltin
{
    public static abstract bool HasMethod(string methodName);

    public static abstract Func<Vm, ObValue[], ObValue> GetMethod(string methodName);
}