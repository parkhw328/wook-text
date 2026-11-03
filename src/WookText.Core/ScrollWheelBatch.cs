namespace WookText.Core;

/// <summary>Combines wheel input into one bounded target without retaining an input queue.</summary>
public sealed class ScrollWheelBatch
{
    private double? _target;

    public void Add(int delta, double currentOffset, double extentHeight, double viewportHeight, double lineHeight, int scrollLines)
    {
        double distance = scrollLines < 0 ? viewportHeight : scrollLines * lineHeight;
        double maximum = Math.Max(0, extentHeight - viewportHeight);
        // Keep fractional wheel deltas and clamp each input, so reversing at an edge is immediate.
        _target = Math.Clamp((_target ?? currentOffset) - delta / 120d * distance, 0, maximum);
    }

    public double? Take()
    {
        double? target = _target;
        Reset();
        return target;
    }

    public void Reset() => _target = null;
}
