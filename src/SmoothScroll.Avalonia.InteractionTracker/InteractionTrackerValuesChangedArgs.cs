using Avalonia;

namespace SmoothScroll.Avalonia.InteractionTracker;

public sealed class InteractionTrackerValuesChangedArgs
{
    internal InteractionTrackerValuesChangedArgs(Vector3D position, double scale, int requestId)
    {
        Position = position;
        Scale = scale;
        RequestId = requestId;
    }

    public Vector3D Position { get; }

    public int RequestId { get; }

    public double Scale { get; }
}
