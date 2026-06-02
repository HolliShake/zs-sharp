namespace obiwan;

public class Cell(ObValue? value)
{
    private int _refCount;
    public ObValue? Value = value;

    public bool IsRef => _refCount > 0;

    public void IncRef()
    {
        _refCount++;
    }

    public void DecRef()
    {
        if (_refCount == 0) return;
        _refCount--;
    }
}