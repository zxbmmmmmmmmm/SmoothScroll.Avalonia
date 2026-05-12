using Avalonia.Rendering.Composition.Animations;

namespace SmoothScroll.Avalonia.Interaction.Modifier;

public class InteractionTrackerInertiaModifier
{
    public ExpressionAnimation? Condition { get; set; }
}

public class InteractionTrackerInertiaMotion
{
    public ExpressionAnimation? Motion { get; set; }
}

internal class ServerInteractionTrackerInertiaModifier
{
    public required ExpressionAnimationInstance Condition { get; set; }
}

internal class ServerInteractionTrackerInertiaMotion : ServerInteractionTrackerInertiaModifier
{
    public required ExpressionAnimationInstance Motion { get; set; }
}
