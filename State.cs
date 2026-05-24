namespace zscript;

public class State
{
    public readonly List<Code> Codes;
    public readonly List<ZsValue> Constants;
    public readonly List<string> Strings;

    public State()
    {
        Constants = [];
        Strings = [];
        Codes = [];
    }

    public int SaveInt(int value)
    {
        var index = Constants.Count;
        Constants.Add(ZsValue.FromInt(value));
        return index;
    }

    public int SaveNum(double value)
    {
        var index = Constants.Count;
        Constants.Add(ZsValue.FromNumber(value));
        return index;
    }

    public int SaveStr(string value)
    {
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