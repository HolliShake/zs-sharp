namespace zscript;

public class Cell(ZsValue? value)
{
    private int _refCount;
    public ZsValue? Value = value;

    public bool IsRef => _refCount > 0;

    public void IncRef()
    {
        _refCount++;
    }

    public void DecRef()
    {
        _refCount--;
    }
}