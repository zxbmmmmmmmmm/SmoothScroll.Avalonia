namespace SmoothScroll.Avalonia.InteractionTracker;

public class InteractionTrackerRequestIgnoredArgs
{
    internal InteractionTrackerRequestIgnoredArgs(int requestId)
        => RequestId = requestId;

    public int RequestId { get; }
}
