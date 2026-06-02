namespace obiwan;

public class Future(FutureState initialState, Frame frame) : IBuiltin
{
    private static readonly string[] Methods = ["then", "error"];
    private readonly List<ObValue> _fullFillReactions = [];
    private readonly List<ObValue> _rejectReactions = [];

    public Future(FutureState initialState, Frame frame, ObValue zsValue)
        : this(initialState, frame)
    {
        Result = zsValue;
    }

    public FutureState State { get; private set; } = initialState;
    public ObValue? Result { get; private set; }

    public Frame SuspendedFrame { get; } = frame;

    public static bool HasMethod(string methodName)
    {
        return Methods.Contains(methodName);
    }

    public static Func<Vm, ObValue[], ObValue> GetMethod(string methodName)
    {
        return methodName switch
        {
            "then" => FutureThenMethod,
            "error" => FutureErrorMethod,
            _ => throw new InvalidSwitchValueException($"method {methodName} not implemented")
        };
    }

    public void FullFill(ObValue zsValue, Queue<ObValue>? queue)
    {
        State = FutureState.Fulfill;
        Result = zsValue;

        if (queue == null)
            return;

        foreach (var reaction in _fullFillReactions)
        {
            var childFut = reaction.Future();
            var frame = childFut.SuspendedFrame;

            frame.PushOperand(Result);
            queue.Enqueue(reaction);
        }

        _fullFillReactions.Clear();
        _rejectReactions.Clear();
    }

    public void Reject(ObValue zsValue, Queue<ObValue>? queue)
    {
        State = FutureState.Rejected;
        Result = zsValue;

        // null means "record the rejection silently — MainLoop will notify listeners
        // at the correct time, after any synchronous error/continue code has run."
        if (queue == null)
            return;

        foreach (var reaction in _rejectReactions)
        {
            var childFut = reaction.Future();
            var frame = childFut.SuspendedFrame;

            frame.PendingError = zsValue;
            frame.Wake();
            queue.Enqueue(reaction);
        }

        _fullFillReactions.Clear();
        _rejectReactions.Clear();
    }

    public void AddListener(ObValue zsValue)
    {
        _fullFillReactions.Add(zsValue);
        _rejectReactions.Add(zsValue);
    }

    private static ObValue FutureThenMethod(Vm vm, ObValue[] arguments)
    {
        if (arguments.Length != 2)
            return ObValue.FromErrorMessage(
                vm.ArgumentErrorClass,
                "arguments must be 2",
                vm.BuildTracebackFromFrame()
            );

        var thisArg = arguments[0];
        var thisArgFut = thisArg.Future();
        var callback = arguments[1];

        var newFrame = new Frame(null, callback, true, false);
        var newPromise = ObValue.FromFuture(new Future(FutureState.Pending, newFrame));
        newFrame.SetFutureOrSkip(newPromise);

        switch (thisArgFut.State)
        {
            case FutureState.Fulfill:
            {
                newFrame.PushOperand(thisArgFut.Result!);

                vm.PendingTasks.Enqueue(newPromise);
                break;
            }
            case FutureState.Rejected:
            {
                // Handled by Error handler
                newPromise.Future().Reject(thisArgFut.Result!, vm.PendingTasks);
                break;
            }
            case FutureState.Pending:
            {
                thisArgFut.AddListener(newPromise);
                break;
            }
            default: throw new InvalidSwitchValueException($"state {thisArgFut.State} not implemented");
        }

        return newPromise;
    }

    private static ObValue FutureErrorMethod(Vm vm, ObValue[] arguments)
    {
        if (arguments.Length != 2)
            return ObValue.FromErrorMessage(
                vm.ArgumentErrorClass,
                "arguments must be 2",
                vm.BuildTracebackFromFrame()
            );

        var thisArg = arguments[0];
        var thisArgFut = thisArg.Future();
        var callback = arguments[1];

        var newFrame = new Frame(null, callback, true, false);
        var newPromise = ObValue.FromFuture(new Future(FutureState.Pending, newFrame));
        newFrame.SetFutureOrSkip(newPromise);

        switch (thisArgFut.State)
        {
            case FutureState.Fulfill:
            {
                newPromise.Future().FullFill(thisArgFut.Result!, vm.PendingTasks);

                break;
            }
            case FutureState.Rejected:
            {
                newFrame.PushOperand(thisArgFut.Result!);

                vm.PendingTasks.Enqueue(newPromise);
                break;
            }
            case FutureState.Pending:
            {
                thisArgFut.AddListener(newPromise);
                break;
            }
            default: throw new InvalidSwitchValueException($"state {thisArgFut.State} not implemented");
        }

        return newPromise;
    }
}