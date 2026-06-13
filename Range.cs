namespace obiwan;

public class Range(double from, double to)
{
    // 1. Check if we are counting up or counting down
    private readonly bool _isAscending = from <= to;
    public readonly double From = from;
    public readonly double To = to;
    private double CurrentCursor { get; set; } = from;

    public double Cursor => CurrentCursor;

    // 2. Dynamically check bounds based on the direction
    // If ascending (0 to 8): check if Cursor < To
    // If descending (8 to 0): check if Cursor > To
    public bool HasNext => _isAscending ? Cursor < To : Cursor > To;

    public double Next(double step = 1)
    {
        var previous = Cursor;
        CurrentCursor += step;
        return previous;
    }

    public double CursorSet(double cursor)
    {
        var previous = Cursor;
        CurrentCursor = cursor;
        return previous;
    }

    // Add a manual reset for when they want to reuse the range later
    public void Reset()
    {
        CurrentCursor = From;
    }
}