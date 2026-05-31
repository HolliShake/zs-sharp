namespace zscript;

public class AutoLoader
{
    private readonly List<AutoLoad> _autoLoads;
    
    public AutoLoader()
    {
        _autoLoads = [
            new AutoLoad(0, "print",ZsValue.FromNativeFunction(Global.Print)),
            new AutoLoad(1, "println",ZsValue.FromNativeFunction(Global.Println)),
            new AutoLoad(2, "scan",ZsValue.FromNativeFunction(Global.Scan)),
        ];
    }

    public void Validate()
    {
        
    }
}