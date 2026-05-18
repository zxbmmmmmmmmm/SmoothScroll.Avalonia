using System.Diagnostics;

namespace SmoothScroll.Avalonia.Interaction;

public class InertiaAxisHelper
{
    private const double DecayRate = 0.95;
    
    public double StartVelocity { get; set; }

    public double NaturalRestingValue { get; set; }

    public double ModifiedRestingValue { get; set; }

    public double CurrentValue { get; set; }

    public double CurrentVelocity { get; set; }


    private readonly Stopwatch _stopwatch = new();
    private double _lastElapsed;

    public double Tick()
    {
        var elapsed = _stopwatch.ElapsedMilliseconds;
        var deltaTime = elapsed - _lastElapsed;
        CurrentValue += CurrentVelocity * deltaTime / 1000;
        CurrentVelocity *= Math.Pow(DecayRate, deltaTime / 10);
        _lastElapsed = elapsed;
        return CurrentValue;
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
