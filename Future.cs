namespace zscript;

public enum FutureState
{
    PENDING,
    FULLFILL,
    REJECTED
}

public class Future
{
    private readonly List<ZsValue> _fullFillReactions = [];
    private readonly List<ZsValue> _rejectReactions = [];

    public Future(FutureState initialState, Frame frame)
    {
        State = initialState;
        Result = null;
        IsReady = false;
        SuspendedFrame = frame;
    }

    public Future(FutureState initialState, Frame frame, ZsValue zsValue) : this(initialState, frame)
    {
        Result = zsValue;
    }

    public FutureState State { get; private set; }
    public ZsValue? Result { get; set; }
    public bool IsReady { get; private set; }
    public Frame SuspendedFrame { get; }

    public void FullFill(ZsValue zsValue, Queue<ZsValue> queue)
    {
        State = FutureState.FULLFILL;
        IsReady = true;
        Result = zsValue;

        foreach (var reaction in _fullFillReactions)
        {
            var childFut = reaction.Future();
            var frame = childFut.SuspendedFrame;

            if (frame.IsCallback)
            {
                frame.PushOperand(Result);
                queue.Enqueue(reaction);
            }
            else
            {
                // Propagate
                childFut.State = FutureState.FULLFILL;
                childFut.Result = Result;
                childFut.IsReady = true;
                frame.PushOperand(Result);
                queue.Enqueue(reaction);
            }
        }

        
        _fullFillReactions.Clear();
        _rejectReactions.Clear();
    }

    public void Reject(ZsValue zsValue, Queue<ZsValue> queue)
    {
        State = FutureState.REJECTED;
        IsReady = true;
        Result = zsValue;

        foreach (var reaction in _rejectReactions)
        {
            var frame = reaction.Future().SuspendedFrame;
            if (frame.IsCallback)
            {
                frame.PushOperand(Result);
                queue.Enqueue(reaction);
            }
            else
            {
                // Propagate
                reaction.Future()
                    .Reject(Result, queue);
            }
        }
    
        _fullFillReactions.Clear();
        _rejectReactions.Clear();
    }

    public void AddListener(ZsValue zsValue)
    {
        _fullFillReactions.Add(zsValue);
        _rejectReactions.Add(zsValue);
    }

    public void AddFullFillReaction(ZsValue zsValue)
    {
        _fullFillReactions.Add(zsValue);
    }

    public void AddRejectReaction(ZsValue zsValue)
    {
        _rejectReactions.Add(zsValue);
    }
    
    //-----------------------
    public static ZsValue FutureThenMethod(Vm vm, ZsValue[] arguments)
    {
        if (arguments.Length != 2)
        {
            return ZsValue.FromErrorMessage(vm.Error, "arguments must be 2");
        }

        var thisArg = arguments[0];
        var thisArgFut = thisArg.Future();
        var callback = arguments[1];

        var newFrame = new Frame(null, callback, true, false);
        var newPromise = ZsValue.FromFuture(new Future(FutureState.PENDING, newFrame));
        newFrame.SetFutureOrSkip(newPromise);
        
        if (thisArgFut.State == FutureState.FULLFILL)
        {
            newFrame.PushOperand(thisArgFut.Result!);
            newPromise.Future()
                .FullFill(thisArgFut.Result!, vm.PendingTasks);
            vm.PendingTasks.Enqueue(newPromise);
        }
        else if (thisArgFut.State ==  FutureState.REJECTED)
        {
            // Handled by Error handler
            newPromise.Future()
                .Reject(thisArgFut.Result!, vm.PendingTasks);
        }
        else if (thisArgFut.State == FutureState.PENDING)
        {
            thisArgFut.AddListener(newPromise);
        }
        
        // Return for chaining
        return newPromise;
    }
    
    public static ZsValue FutureCatchMethod(Vm vm, ZsValue[] arguments)
    {
        if (arguments.Length != 2)
        {
            return ZsValue.FromErrorMessage(vm.Error, "arguments must be 2");
        }

        var thisArg = arguments[0];
        var thisArgFut = thisArg.Future();
        var callback = arguments[1];

        var newFrame = new Frame(null, callback, true, false);
        var newPromise = ZsValue.FromFuture(new Future(FutureState.PENDING, newFrame));
        newFrame.SetFutureOrSkip(newPromise);
        
        if (thisArgFut.State == FutureState.FULLFILL)
        {
            newPromise.Future()
                .FullFill(thisArgFut.Result!, vm.PendingTasks);
            vm.PendingTasks.Enqueue(newPromise);
        }
        else if (thisArgFut.State ==  FutureState.REJECTED)
        {
            newFrame.PushOperand(thisArgFut.Result!);
            newPromise.Future()
                .Reject(thisArgFut.Result!, vm.PendingTasks);
            
        }
        else if (thisArgFut.State == FutureState.PENDING)
        {
            thisArgFut.AddListener(newPromise);
        }
        
        // Return for chaining
        return newPromise;
    }
}