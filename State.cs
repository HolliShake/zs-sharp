namespace obiwan;

public class State : IDisposable
{
    private readonly Stack<string> _dirStack = new();
    public readonly AutoLoader AutoLoader = new();
    public readonly List<Code> Codes = [];
    public readonly List<ObValue> Constants = [];
    public readonly Dictionary<string, ObValue?> LoadedModules = new();
    public readonly List<string> ModuleNames = [];

    public void Dispose()
    {
        foreach (var code in Codes) code.Dispose();
        _dirStack.Clear();
        Constants.Clear();
        Codes.Clear();
        LoadedModules.Clear();
    }

    public void PushDir(string dir)
    {
        _dirStack.Push(dir);
    }

    public string PeekDir()
    {
        return _dirStack.Peek();
    }

    public string PopDir()
    {
        return _dirStack.Pop();
    }

    public int RegisterModuleName(string moduleName)
    {
        var index = ModuleNames.Count;
        ModuleNames.Add(moduleName);
        return index;
    }

    public int SaveInt(int value)
    {
        var existingIndex = Constants.FindIndex(x => x.Type == ValueType.Int && x.Int() == value);
        if (existingIndex != -1) return existingIndex;

        var index = Constants.Count;
        Constants.Add(ObValue.FromInt(value));
        return index;
    }

    public int SaveNum(double value)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        var existingIndex = Constants.FindIndex(x => x.Type == ValueType.Number && x.Number() == value);
        if (existingIndex != -1) return existingIndex;

        var index = Constants.Count;
        Constants.Add(ObValue.FromNumber(value));
        return index;
    }

    public int SaveStr(string value)
    {
        var existingIndex = Constants.FindIndex(x => x.Type == ValueType.String && x.Ref!.Equals(value));
        if (existingIndex != -1) return existingIndex;

        var index = Constants.Count;
        Constants.Add(ObValue.FromString(value));
        return index;
    }

    public int SaveCodeTemplate(Code code)
    {
        var address = Codes.Count;
        Codes.Add(code);
        return address;
    }
}