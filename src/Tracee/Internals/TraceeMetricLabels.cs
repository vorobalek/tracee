namespace Tracee.Internals;

internal sealed record TraceeMetricLabels(
    int StackId, 
    string Key, 
    int Depth,
    long Created) : ITraceeMetricLabels
{
    public int StackId { get; } = StackId;
    public string Key { get; } = Key;
    public int Depth { get; } = Depth;
    public long Created { get; } = Created;

    public TraceeMetricLabels(TraceeMetricLabels origin)
    {
        StackId = origin.StackId;
        Key = origin.Key;
        Depth = origin.Depth;
        Created = origin.Created;
    }
}