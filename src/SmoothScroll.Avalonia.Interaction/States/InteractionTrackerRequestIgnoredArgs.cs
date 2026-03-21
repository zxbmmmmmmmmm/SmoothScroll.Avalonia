namespace SmoothScroll.Avalonia.Interaction;

public class InteractionTrackerRequestIgnoredArgs
{
    internal InteractionTrackerRequestIgnoredArgs(int requestId)
        => RequestId = requestId;

    public int RequestId { get; }
}
