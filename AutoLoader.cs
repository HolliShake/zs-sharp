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
            new AutoLoad(1, "Path", Global.BuildPath),
            new AutoLoad(2, "File", Global.BuildFile),
            new AutoLoad(3, "import", _ => ObValue.FromNativeFunction(Global.Import)),
            new AutoLoad(4, "provide", _ => ObValue.FromNativeFunction(Global.Provide)),
            new AutoLoad(5, "inject", _ => ObValue.FromNativeFunction(Global.Inject)),
            new AutoLoad(6, "print", _ => ObValue.FromNativeFunction(Global.Print)),
            new AutoLoad(7, "println", _ => ObValue.FromNativeFunction(Global.Println)),
            new AutoLoad(8, "clear", _ => ObValue.FromNativeFunction(Global.Clear)),
            new AutoLoad(9, "home", _ => ObValue.FromNativeFunction(Global.Home)),
            new AutoLoad(10, "scan", _ => ObValue.FromNativeFunction(Global.Scan)),
            new AutoLoad(11, "isWindows", _ => ObValue.FromNativeFunction(Global.OsWin)),
            new AutoLoad(12, "isMac", _ => ObValue.FromNativeFunction(Global.OsMac)),
            new AutoLoad(13, "isLinux", _ => ObValue.FromNativeFunction(Global.OsLinux)),
            new AutoLoad(14, "getOsType", _ => ObValue.FromNativeFunction(Global.GetOsType))
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
            symbolTable.Add(name, address, true, false, new Position(1, 1));
        }
    }

    public void InjectObject(Vm vm, Frame frame)
    {
        foreach (var autoLoad in _autoLoads)
            frame.SetEnvVar(autoLoad.Address, autoLoad.Callback(vm));
    }
}