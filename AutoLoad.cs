namespace obiwan;

public class AutoLoad(int address, string name, Func<Vm, ObValue> callback)
{
    public readonly int Address = address;
    public readonly Func<Vm, ObValue> Callback = callback;
    public readonly string Name = name;
}