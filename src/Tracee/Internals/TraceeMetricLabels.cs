namespace Tracee.Internals;

internal sealed record TraceeMetricLabels(
    string Key,
    int Depth,
    long Created) : ITraceeMetricLabels
{
    public TraceeMetricLabels(ITraceeMetricLabels origin)
        : this(origin.Key, origin.Depth, origin.Created)
    {
    }

    public string Key { get; } = Key;
    public int Depth { get; } = Depth;
    public long Created { get; } = Created;
}