namespace Tracee;

public interface ITraceeMetricLabels
{
    string Key { get; }

    int Depth { get; }

    long Created { get; }
}