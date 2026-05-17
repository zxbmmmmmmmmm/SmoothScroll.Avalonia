using System.Diagnostics;

namespace SmoothScroll.Avalonia.Interaction;

public class InertiaAxisHelper
{
    private const double DecayRate = 0.95;
    
    public double StartVelocity { get; set; }

    public double NaturalRestingValue { get; set; }

    public double ModifiedRestingValue { get; set; }

    public double CurrentValue { get; set; }


    private readonly Stopwatch _stopwatch = new();

    public double Tick()
    {
        var elapsed = _stopwatch.ElapsedMilliseconds;

    }

    public void Initialize(
        double currentValue,
        double startVelocity)
    {
        _stopwatch.Start();
        CurrentValue = currentValue;
        StartVelocity = startVelocity;
    }
}
