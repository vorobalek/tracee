using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Tracee.AspNetCore.Options;

namespace Tracee.AspNetCore.Middleware;

internal sealed class TraceeMiddleware(
    ITracee tracee,
    IOptions<TraceeOptions> options) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        using (tracee.Scope(options.Value.Key))
        {
            await next(context);
        }

        if (options.Value.LogLevel.HasValue)
            tracee.Log(options.Value.LogLevel.Value, BuildPrettyLog(tracee.Collect()));
    }

    private static string BuildPrettyLog(IReadOnlyDictionary<ITraceeMetricKey, ITraceeMetricValue> metrics)
    {
        var minDepth = metrics.Min(metric => metric.Key.Depth);
        var prepared = metrics
            .Select(metric =>
            (
                Key:
                $"{(metric.Key.Depth - minDepth > 0 ? new string('–', metric.Key.Depth - minDepth - 1) + ' ' : string.Empty)}{metric.Key.Key}",
                Value: $"{metric.Value.Milliseconds} ms"
            )).ToArray();
        var (paddingKey, paddingValue) = prepared
            .Aggregate(
                (0, 0),
                (padding, next) =>
                {
                    var (paddingKey, paddingValue) = padding;
                    return (
                        Math.Max(paddingKey, next.Key.Length),
                        Math.Max(paddingValue, next.Value.Length));
                });
        var stringBuilder = new StringBuilder();
        foreach (var (key, value) in prepared)
            stringBuilder.AppendLine($"| {key.PadRight(paddingKey)} | {value.PadLeft(paddingValue)} |");
        return stringBuilder.ToString();
    }
}