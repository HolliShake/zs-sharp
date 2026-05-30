namespace zscript;

public class State
{
    public readonly List<Code> Codes = [];
    public readonly List<ZsValue> Constants = [];
    public readonly List<string> ModuleNames = [];

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
        Constants.Add(ZsValue.FromInt(value));
        return index;
    }

    public int SaveNum(double value)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        var existingIndex = Constants.FindIndex(x => x.Type == ValueType.Number && x.Number() == value);
        if (existingIndex != -1) return existingIndex;

        var index = Constants.Count;
        Constants.Add(ZsValue.FromNumber(value));
        return index;
    }

    public int SaveStr(string value)
    {
        var existingIndex = Constants.FindIndex(x => x.Type == ValueType.String && x.Ref!.Equals(value));
        if (existingIndex != -1) return existingIndex;

        var index = Constants.Count;
        Constants.Add(ZsValue.FromString(value));
        return index;
    }

    public int SaveCodeTemplate(Code code)
    {
        var address = Codes.Count;
        Codes.Add(code);
        return address;
    }
}