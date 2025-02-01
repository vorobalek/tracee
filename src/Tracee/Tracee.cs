using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Tracee.Internals;

namespace Tracee;

[DebuggerDisplay("{Key} ({Milliseconds} ms)")]
public sealed class Tracee : ITracee
{
    private static readonly AsyncLocal<Stack<Tracee>?> TraceStack = new();

    private readonly Tracee? _parent;
    private readonly string _keySplit;
    private readonly bool _ignoreNested;
    private readonly Stopwatch _stopwatch;
    private readonly ConcurrentDictionary<TraceeMetricLabels, TraceeMetricValue> _synced = new();

    private bool _disposed;

    private Tracee(
        string key,
        string keySplit,
        bool ignoreNested,
        string loggerCategoryName,
        Tracee? parent)
    {
        Key = key;
        _keySplit = keySplit;
        _ignoreNested = ignoreNested;
        LoggerCategoryName = loggerCategoryName;
        _parent = parent;

        Depth = (_parent?.Depth ?? 0) + 1;
        Created = Stopwatch.GetTimestamp();

        _stopwatch = Stopwatch.StartNew();

        TraceStack.Value ??= new Stack<Tracee>();
        TraceStack.Value.Push(this);
    }

    public static ITracee Create(
        string key,
        string keySplit = "_",
        bool ignoreNested = false,
        string? loggerCategoryName = null)
    {
        TraceStack.Value = null;

        loggerCategoryName ??= "Tracee";
        return new Tracee(
            key,
            keySplit,
            ignoreNested,
            loggerCategoryName,
            parent: null);
    }

    private static Tracee? CurrentTop
    {
        get
        {
            var stack = TraceStack.Value;
            return stack is
            {
                Count: > 0
            }
                ? stack.Peek()
                : null;
        }
    }

    public string Key { get; }
    public int Depth { get; }
    public long Created { get; }
    public long Milliseconds => _stopwatch.ElapsedMilliseconds;

    private string LoggerCategoryName { get; }

    public ITracee Scoped(
        string? key = null,
        [CallerMemberName] string memberName = "",
        bool ignoreNested = false)
    {
        var parent = CurrentTop ?? this;

        if (parent._ignoreNested)
            return parent;

        var finalKey = !string.IsNullOrWhiteSpace(key)
            ? $"{parent.Key}{_keySplit}{key}"
            : $"{parent.Key}{_keySplit}{memberName}";

        return new Tracee(
            finalKey,
            _keySplit,
            ignoreNested,
            parent.LoggerCategoryName,
            parent);
    }

    public ITracee Fixed(string key)
    {
        var parent = CurrentTop ?? this;
        if (parent._ignoreNested) return parent;

        var finalKey = $"{parent.Key}{_keySplit}{key}";
        return new Tracee(
            finalKey,
            _keySplit,
            ignoreNested: false,
            parent.LoggerCategoryName,
            parent);
    }

    public IReadOnlyDictionary<ITraceeMetricLabels, ITraceeMetricValue> Collect()
    {
        return _synced
            .ToDictionary(
                ITraceeMetricLabels (pair) => new TraceeMetricLabels(pair.Key),
                ITraceeMetricValue (pair) => new TraceeMetricValue(pair.Value));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stopwatch.Stop();

        if (TraceStack.Value is
            {
                Count: > 0
            }
            && ReferenceEquals(TraceStack.Value.Peek(), this))
        {
            TraceStack.Value.Pop();
        }

        foreach (var kvp in _synced)
        {
            _parent?._synced.AddOrUpdate(
                kvp.Key,
                _ => new TraceeMetricValue(kvp.Value),
                (_, existing) => existing + kvp.Value);
        }

        var label = new TraceeMetricLabels(Key, Depth, Created);
        _parent?._synced.AddOrUpdate(
            label,
            _ => new TraceeMetricValue(Milliseconds),
            (_, existing) => existing.AddMilliseconds(Milliseconds));
    }
}