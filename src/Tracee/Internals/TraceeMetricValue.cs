namespace Tracee.Internals;

internal sealed record TraceeMetricValue(long Milliseconds) : ITraceeMetricValue
{
    public TraceeMetricValue(ITraceeMetricValue origin)
        : this(origin.Milliseconds)
    {
    }

    public static TraceeMetricValue operator +(TraceeMetricValue current, TraceeMetricValue newValue)
    {
        return new TraceeMetricValue(current.Milliseconds + newValue.Milliseconds);
    }

    public long Milliseconds { get; } = Milliseconds;

    internal TraceeMetricValue AddMilliseconds(long milliseconds)
    {
        return new TraceeMetricValue(Milliseconds + milliseconds);
    }
}