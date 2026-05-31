namespace zscript;

public class AutoLoader
{
    private readonly List<AutoLoad> _autoLoads;
    public readonly int InjectedCount;

    public AutoLoader()
    {
        _autoLoads =
        [
            new AutoLoad(0, "Math", vm => Global.BuildMath(vm)),
            new AutoLoad(1, "import", vm => ZsValue.FromNativeFunction(Global.Import)),
            new AutoLoad(2, "print", vm => ZsValue.FromNativeFunction(Global.Print)),
            new AutoLoad(3, "println", vm => ZsValue.FromNativeFunction(Global.Println)),
            new AutoLoad(4, "scan", vm => ZsValue.FromNativeFunction(Global.Scan))
        ];

        InjectedCount = _autoLoads.Count;
    }

    public void Validate()
    {
    }

    public void InjectSymbol(SymbolTable symbolTable)
    {
        foreach (var (address, name) in _autoLoads.ToDictionary(x => x.Address, x => x.Name))
        {
            if (symbolTable.AlreadyExists(name)) throw new Exception($"Autoload {name} already exists");
            symbolTable.Add(name, address, true, new Position(1, 1));
        }
    }

    public void InjectObject(Vm vm, Frame frame)
    {
        foreach (var autoLoad in _autoLoads)
            frame.SetEnvVar(autoLoad.Address, autoLoad.Callback(vm));
    }
}