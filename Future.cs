namespace zscript;

public enum FutureState
{
    Pending,
    Fulfill,
    Rejected
}

public class Future(FutureState initialState, Frame frame)
{
    private readonly List<ZsValue> _fullFillReactions = [];
    private readonly List<ZsValue> _rejectReactions = [];

    public Future(FutureState initialState, Frame frame, ZsValue zsValue) : this(initialState, frame)
    {
        Result = zsValue;
    }

    public FutureState State { get; private set; } = initialState;
    public ZsValue? Result { get; private set; }
    public bool IsReady { get; private set; }
    public Frame SuspendedFrame { get; } = frame;

    public void FullFill(ZsValue zsValue, Queue<ZsValue> queue)
    {
        State = FutureState.Fulfill;
        IsReady = true;
        Result = zsValue;

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

    private void Reject(ZsValue zsValue, Queue<ZsValue> queue)
    {
        State = FutureState.Rejected;
        IsReady = true;
        Result = zsValue;

        foreach (var reaction in _rejectReactions)
        {
            var childFut = reaction.Future();
            var frame = childFut.SuspendedFrame;

            frame.PushOperand(Result);
            queue.Enqueue(reaction);
        }

        _fullFillReactions.Clear();
        _rejectReactions.Clear();
    }

    public void AddListener(ZsValue zsValue)
    {
        _fullFillReactions.Add(zsValue);
        _rejectReactions.Add(zsValue);
    }

    public static ZsValue FutureThenMethod(Vm vm, ZsValue[] arguments)
    {
        if (arguments.Length != 2) return ZsValue.FromErrorMessage(vm.Error, "arguments must be 2");

        var thisArg = arguments[0];
        var thisArgFut = thisArg.Future();
        var callback = arguments[1];

        var newFrame = new Frame(null, callback, true, false);
        var newPromise = ZsValue.FromFuture(new Future(FutureState.Pending, newFrame));
        newFrame.SetFutureOrSkip(newPromise);

        switch (thisArgFut.State)
        {
            case FutureState.Fulfill:
            {
                newFrame.PushOperand(thisArgFut.Result!);
                newPromise.Future()
                    .FullFill(thisArgFut.Result!, vm.PendingTasks);
                vm.PendingTasks.Enqueue(newPromise);
                break;
            }
            case FutureState.Rejected:
            {
                // Handled by Error handler
                newPromise.Future()
                    .Reject(thisArgFut.Result!, vm.PendingTasks);
                break;
            }
            case FutureState.Pending:
            {
                thisArgFut.AddListener(newPromise);
                break;
            }
        }

        // Return for chaining
        return newPromise;
    }

    public static ZsValue FutureCatchMethod(Vm vm, ZsValue[] arguments)
    {
        if (arguments.Length != 2) return ZsValue.FromErrorMessage(vm.Error, "arguments must be 2");

        var thisArg = arguments[0];
        var thisArgFut = thisArg.Future();
        var callback = arguments[1];

        var newFrame = new Frame(null, callback, true, false);
        var newPromise = ZsValue.FromFuture(new Future(FutureState.Pending, newFrame));
        newFrame.SetFutureOrSkip(newPromise);

        switch (thisArgFut.State)
        {
            case FutureState.Fulfill:
            {
                newPromise.Future()
                    .FullFill(thisArgFut.Result!, vm.PendingTasks);
                vm.PendingTasks.Enqueue(newPromise);
                break;
            }
            case FutureState.Rejected:
            {
                newFrame.PushOperand(thisArgFut.Result!);
                newPromise.Future()
                    .Reject(thisArgFut.Result!, vm.PendingTasks);
                break;
            }
            case FutureState.Pending:
            {
                thisArgFut.AddListener(newPromise);
                break;
            }
        }

        // Return for chaining
        return newPromise;
    }
}