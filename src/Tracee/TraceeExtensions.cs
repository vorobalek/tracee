using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tracee;

public static class TraceeExtensions
{
    public static void CollectAll(this ITracee tracee,
        ILogger logger,
        LogLevel logLevel)
    {
        var metrics = tracee.Collect();
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        logger.Log(logLevel, BuildPrettyLog(metrics));
    }

    private static string BuildPrettyLog(IReadOnlyDictionary<ITraceeMetricLabels, ITraceeMetricValue> metrics)
    {
        if (metrics.Count == 0) return string.Empty;

        var minDepth = metrics.Min(metric => metric.Key.Depth);
        var prepared = metrics
            .GroupBy(metric => metric.Key.Key)
            .Select(group => new
            {
                group.Key,
                Created = group.Min(metric => metric.Key.Created),
                Depth = group.Min(metric => metric.Key.Depth),
                Value = group.Sum(metric => metric.Value.Milliseconds)
            })
            .OrderBy(metric => metric.Created)
            .ThenBy(metric => metric.Key)
            .Select(metric =>
            (
                Key: $"{(
                    metric.Depth - minDepth > 0
                        ? new string('.', metric.Depth - minDepth)
                        : string.Empty
                )}{metric.Key}",
                Value: $"{metric.Value} ms"
            ))
            .ToArray();

        var (metricsTitle, durationTitle) = ("Metric", "Duration (ms)");
        
        var (paddingKey, paddingValue) = prepared
            .Aggregate(
                (metricsTitle.Length, durationTitle.Length),
                (padding, next) =>
                {
                    var (pk, pv) = padding;
                    return (
                        Math.Max(pk, next.Key.Length),
                        Math.Max(pv, next.Value.Length)
                    );
                });

        var sb = new StringBuilder();
        sb.AppendLine($"| {metricsTitle.PadRight(paddingKey)} | {durationTitle.PadLeft(paddingValue)} |");
        sb.AppendLine($"|{new string('–', paddingKey + paddingValue + 5)}|");

        foreach (var (key, value) in prepared)
        {
            sb.AppendLine($"| {key.PadRight(paddingKey)} | {value.PadLeft(paddingValue)} |");
        }

        return sb.ToString();
    }
}