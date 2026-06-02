namespace obiwan;

public class AutoLoader
{
    private readonly List<AutoLoad> _autoLoads;
    public readonly int InjectedCount;

    public AutoLoader()
    {
        _autoLoads =
        [
            new AutoLoad(0, "Math", Global.BuildMath),
            new AutoLoad(1, "import", _ => ObValue.FromNativeFunction(Global.Import)),
            new AutoLoad(2, "print", _ => ObValue.FromNativeFunction(Global.Print)),
            new AutoLoad(3, "println", _ => ObValue.FromNativeFunction(Global.Println)),
            new AutoLoad(4, "scan", _ => ObValue.FromNativeFunction(Global.Scan)),
            new AutoLoad(5, "isWindows", _ => ObValue.FromNativeFunction(Global.OsWin)),
            new AutoLoad(6, "isMac", _ => ObValue.FromNativeFunction(Global.OsMac)),
            new AutoLoad(7, "isLinux", _ => ObValue.FromNativeFunction(Global.OsLinux)),
            new AutoLoad(8, "getOsType", _ => ObValue.FromNativeFunction(Global.GetOsType))
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