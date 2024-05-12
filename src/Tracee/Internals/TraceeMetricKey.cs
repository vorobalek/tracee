namespace Tracee.Internals;

internal sealed record TraceeMetricKey(int StackId, string Key, int Depth) : ITraceeMetricKey
{
    public int StackId { get; } = StackId;
    public string Key { get; } = Key;
    public int Depth { get; } = Depth;
}