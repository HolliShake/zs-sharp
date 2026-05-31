namespace zscript;

public class AutoLoad(int address, string name, Func<Vm, ZsValue> callback)
{
    public readonly int Address = address;
    public readonly Func<Vm, ZsValue> Callback = callback;
    public readonly string Name = name;
}