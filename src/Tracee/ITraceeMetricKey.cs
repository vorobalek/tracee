namespace Tracee;

public interface ITraceeMetricKey
{
    int StackId { get; }

    string Key { get; }

    int Depth { get; }
}