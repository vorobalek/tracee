namespace Tracee.Internals;

internal sealed record TraceeMetricValue(long Milliseconds) : ITraceeMetricValue
{
    public TraceeMetricValue(TraceeMetricValue origin)
    {
        Milliseconds = origin.Milliseconds;
    }

    public long Milliseconds { get; } = Milliseconds;

    public static TraceeMetricValue operator +(TraceeMetricValue current, TraceeMetricValue newValue)
    {
        return new TraceeMetricValue(current.Milliseconds + newValue.Milliseconds);
    }

    internal TraceeMetricValue AddMilliseconds(long milliseconds)
    {
        return new TraceeMetricValue(Milliseconds + milliseconds);
    }
}