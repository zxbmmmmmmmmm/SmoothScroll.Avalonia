namespace SmoothScroll.Avalonia.Interaction;


/// <summary>
/// Defines the chaining behavior for a VisualInteractionSource. 
/// There are three options: Always chain, never chain or auto chain (let the system choose).
/// If chaining is enabled, when an InteractionTracker reaches its minimum or maximum bounds,
/// it will instead send the input to the next ancestor VisualInteractionSource.</summary>
public enum InteractionChainingMode
{
    /// <summary>
    /// Automatically determine whether to continue the manipulation.
    /// </summary>
    Auto,
    /// <summary>
    /// Always continue the manipulation.
    /// </summary>
    Always,
    /// <summary>
    /// Never continue the manipulation.
    /// </summary>
    Never,
}
