namespace zscript;

public class AutoLoader
{
    private readonly List<AutoLoad> _autoLoads;
    public readonly int InjectedCount;

    public AutoLoader()
    {
        _autoLoads =
        [
            new AutoLoad(0, "print", ZsValue.FromNativeFunction(Global.Print)),
            new AutoLoad(1, "println", ZsValue.FromNativeFunction(Global.Println)),
            new AutoLoad(2, "scan", ZsValue.FromNativeFunction(Global.Scan))
        ];

        InjectedCount = _autoLoads.Count;
    }

    public void Validate()
    {
    }

    public void InjectSymbol(SymbolTable symbolTable)
    {
        foreach (var autoLoad in _autoLoads)
        {
            if (symbolTable.AlreadyExists(autoLoad.Name)) throw new Exception("Autoload already exists");
            symbolTable.Add(autoLoad.Name, autoLoad.Address, true, new Position(1, 1));
        }
    }

    public void InjectObject(Frame frame)
    {
        foreach (var autoLoad in _autoLoads)
            frame.SetEnvVar(autoLoad.Address, autoLoad.Value);
    }
}