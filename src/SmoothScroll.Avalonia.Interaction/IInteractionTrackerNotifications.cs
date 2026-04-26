using Avalonia;

namespace SmoothScroll.Avalonia.Interaction;

internal interface IInteractionTrackerNotifications
{
    void NotifyValuesChangedFromServer(Vector3D position, double scale, int requestId);
    void NotifyCustomAnimationStateEnteredFromServer();
    void NotifyIdleStateEnteredFromServer(int requestId, bool isFromBinding);
    void NotifyInertiaStateEnteredFromServer(InteractionTrackerInertiaStateEnteredArgs args);
    void NotifyInteractingStateEnteredFromServer(int requestId, bool isFromBinding);
    void NotifyRequestIgnoredFromServer(int requestId);
}
