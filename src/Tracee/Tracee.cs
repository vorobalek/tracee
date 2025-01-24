using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tracee.Internals;

namespace Tracee;

[DebuggerDisplay("{InstanceIdString}.{StackId}.{Key} ({Milliseconds} ms)")]
public sealed class Tracee : ITracee
{
    private readonly Guid _instanceId = Guid.NewGuid();

    private readonly string _loggerCategoryName;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Tracee? _parent;

    private readonly ConcurrentStack<Tracee> _stack;
    private readonly ConcurrentDictionary<int, ConcurrentStack<Tracee>> _stackPool;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private readonly ConcurrentDictionary<TraceeMetricLabels, TraceeMetricValue> _synced = new();

    private bool _disposed;

    private Tracee(
        string key,
        string keySplit,
        bool ignoreNested,
        string? loggerCategoryName,
        ILoggerFactory loggerFactory,
        ConcurrentDictionary<int, ConcurrentStack<Tracee>> stackPool,
        Tracee? parent)
    {
        StackId = CurrentStackId;

        Key = key;
        KeySplit = keySplit;
        IgnoreNested = ignoreNested;
        _loggerCategoryName = loggerCategoryName ?? $"Tracee.{_instanceId:N}";
        _loggerFactory = loggerFactory;
        _stackPool = stackPool;
        _parent = parent;

        Depth = (_parent?.Depth ?? 0) + 1;
        Created = Stopwatch.GetTimestamp();
        _stack = _stackPool.AddOrUpdate(
            StackId,
            _ => [],
            (_, stack) => stack);
        Logger = _loggerFactory.CreateLogger(_loggerCategoryName);
        Logger.LogTrace(
            "[created {Milliseconds} ms]\t{InstanceId:N}.{Stack}.{Key}",
            _stopwatch.ElapsedMilliseconds,
            _instanceId,
            StackId,
            Key);
    }

    private string InstanceIdString => $"{_instanceId:N}";
    private static int CurrentStackId => Task.CurrentId ?? 0;

    private ConcurrentStack<Tracee> StackToAttach =>
        _stackPool.ContainsKey(CurrentStackId) &&
        !_stackPool[CurrentStackId].IsEmpty
            ? _stackPool[CurrentStackId]
            : _stack;

    private string KeySplit { get; }
    private bool IgnoreNested { get; }

    public int StackId { get; }
    public string Key { get; }
    public int Depth { get; }
    public long Created { get; }

    public long Milliseconds => _stopwatch.ElapsedMilliseconds;

    public ILogger Logger { get; }

    public ITracee Scoped(
        string? key = null,
        [CallerMemberName] string memberName = "",
        bool ignoreNested = false)
    {
        if (StackToAttach.TryPeek(out var peek) && peek._instanceId != _instanceId)
            return peek.Scoped(key, memberName, ignoreNested);

        if (IgnoreNested) return this;

        var prefix = !string.IsNullOrWhiteSpace(Key)
            ? $"{Key}_"
            : string.Empty;

        key = !string.IsNullOrWhiteSpace(key)
            ? $"{prefix}{key}"
            : !string.IsNullOrWhiteSpace(memberName)
                ? $"{prefix}{memberName}"
                : $"{prefix}{Guid.NewGuid():N}";

        var tracee = new Tracee(
            key,
            KeySplit,
            ignoreNested,
            _loggerCategoryName,
            _loggerFactory,
            _stackPool,
            this);

        tracee._stack.Push(tracee);
        return tracee;
    }

    public ITracee Fixed(string key)
    {
        if (_parent != null)
            return _parent.Fixed(key);

        var tracee = new Tracee(
            key,
            KeySplit,
            false,
            _loggerCategoryName,
            _loggerFactory,
            new ConcurrentDictionary<int, ConcurrentStack<Tracee>>(
                [new KeyValuePair<int, ConcurrentStack<Tracee>>(0, [])]),
            this);

        return tracee;
    }

    public IReadOnlyDictionary<ITraceeMetricLabels, ITraceeMetricValue> Collect()
    {
        return _synced
            .ToDictionary<KeyValuePair<TraceeMetricLabels, TraceeMetricValue>, ITraceeMetricLabels,
                ITraceeMetricValue>(
                item => new TraceeMetricLabels(item.Key),
                item => new TraceeMetricValue(item.Value));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            while (_stack.TryPeek(out var peek))
                if (peek._instanceId != _instanceId)
                {
                    if (peek.Depth > Depth)
                    {
                        _stack.TryPop(out var tracee);
                        tracee.Dispose();
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    _stack.TryPop(out _);
                    break;
                }

            _stopwatch.Stop();

            foreach (var metrics in _synced)
                _parent?._synced.AddOrUpdate(
                    metrics.Key,
                    key => SyncCreateValue(
                        key,
                        metrics.Value),
                    (key, value) => SyncUpdateValue(
                        key,
                        value,
                        value + metrics.Value));

            _parent?._synced.AddOrUpdate(
                new TraceeMetricLabels(StackId, Key, Depth, Created),
                key => SyncCreateValue(
                    key,
                    new TraceeMetricValue(_stopwatch.ElapsedMilliseconds)),
                (key, value) => SyncUpdateValue(
                    key,
                    value,
                    value.AddMilliseconds(_stopwatch.ElapsedMilliseconds)));

            Logger.LogTrace(
                "[disposed {Milliseconds} ms]\t{InstanceId:N}.{Stack}.{Key}",
                _stopwatch.ElapsedMilliseconds,
                _instanceId,
                StackId,
                Key);
        }

        _disposed = true;
    }

    public void Log(LogLevel logLevel, string message)
    {
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        Logger.Log(logLevel, message);
    }

    public static ITracee Create(
        string key,
        ILoggerFactory loggerFactory,
        string keySplit = "_",
        bool ignoreNested = false,
        string? loggerCategoryName = null)
    {
        return new Tracee(
            key,
            keySplit,
            ignoreNested,
            loggerCategoryName,
            loggerFactory,
            new ConcurrentDictionary<int, ConcurrentStack<Tracee>>(
                [new KeyValuePair<int, ConcurrentStack<Tracee>>(0, [])]),
            null);
    }

    private TraceeMetricValue SyncCreateValue(
        TraceeMetricLabels labels,
        TraceeMetricValue value)
    {
        Logger.LogTrace(
            "[sync created] {Stack}.{Key}\t({Milliseconds} ms)",
            labels.StackId,
            labels.Key,
            value.Milliseconds);
        return value;
    }

    private TraceeMetricValue SyncUpdateValue(
        TraceeMetricLabels labels,
        TraceeMetricValue currentValue,
        TraceeMetricValue newValue)
    {
        Logger.LogTrace(
            "[sync updated] {Stack}.{Key}\t({Milliseconds} ms) -> ({NewMilliseconds} ms)",
            labels.StackId,
            labels.Key,
            currentValue.Milliseconds,
            newValue.Milliseconds);
        return newValue;
    }
}