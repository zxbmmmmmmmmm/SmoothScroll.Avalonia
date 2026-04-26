using Avalonia;
using Avalonia.Threading;

namespace SmoothScroll.Avalonia.Interaction;

internal sealed class InteractionTrackerNotification : IInteractionTrackerNotifications
{
    private readonly InteractionTracker _tracker;

    public InteractionTrackerNotification(InteractionTracker tracker)
    {
        _tracker = tracker;
    }

    public void NotifyValuesChangedFromServer(Vector3D position, double scale, int requestId)
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                _tracker.Position = position;
                _tracker.Scale = scale;
                _tracker.Owner?.ValuesChanged(_tracker, new InteractionTrackerValuesChangedArgs(position, scale, requestId));
            },
            DispatcherPriority.Render);
    }

    public void NotifyCustomAnimationStateEnteredFromServer()
    {
        Dispatcher.UIThread.Post(
            () => _tracker.Owner?.CustomAnimationStateEntered(_tracker, new InteractionTrackerCustomAnimationStateEnteredArgs()),
            DispatcherPriority.Render);
    }

    public void NotifyIdleStateEnteredFromServer(int requestId, bool isFromBinding)
    {
        Dispatcher.UIThread.Post(
            () => _tracker.Owner?.IdleStateEntered(_tracker, new InteractionTrackerIdleStateEnteredArgs(requestId, isFromBinding)),
            DispatcherPriority.Render);
    }

    public void NotifyInertiaStateEnteredFromServer(InteractionTrackerInertiaStateEnteredArgs args)
    {
        Dispatcher.UIThread.Post(() => _tracker.Owner?.InertiaStateEntered(_tracker, args), DispatcherPriority.Render);
    }

    public void NotifyInteractingStateEnteredFromServer(int requestId, bool isFromBinding)
    {
        Dispatcher.UIThread.Post(
            () => _tracker.Owner?.InteractingStateEntered(_tracker, new InteractionTrackerInteractingStateEnteredArgs(requestId, isFromBinding)),
            DispatcherPriority.Render);
    }

    public void NotifyRequestIgnoredFromServer(int requestId)
    {
        Dispatcher.UIThread.Post(
            () => _tracker.Owner?.RequestIgnored(_tracker, new InteractionTrackerRequestIgnoredArgs(requestId)),
            DispatcherPriority.Render);
    }
}
