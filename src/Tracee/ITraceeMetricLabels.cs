namespace Tracee;

public interface ITraceeMetricLabels
{
    int StackId { get; }

    string Key { get; }

    int Depth { get; }

    long Created { get; }
}