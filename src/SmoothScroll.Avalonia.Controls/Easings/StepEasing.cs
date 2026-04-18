using Avalonia.Animation.Easings;

namespace SmoothScroll.Avalonia.Controls.Easings;


internal class StepEasing : Easing
{
    public override double Ease(double progress)
    {
        return progress < 0.5 ? 0 : 1;
    }
}
