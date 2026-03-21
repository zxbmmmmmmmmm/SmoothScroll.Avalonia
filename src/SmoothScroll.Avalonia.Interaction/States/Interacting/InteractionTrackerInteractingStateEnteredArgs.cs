namespace SmoothScroll.Avalonia.Interaction;

public partial class InteractionTrackerInteractingStateEnteredArgs
{
    internal InteractionTrackerInteractingStateEnteredArgs(int requestId, bool isFromBinding)
    {
        RequestId = requestId;
        IsFromBinding = isFromBinding;
    }

    public int RequestId { get; }

    public bool IsFromBinding { get; }
}
