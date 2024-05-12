using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tracee.Internals;

namespace Tracee;

[DebuggerDisplay("{InstanceIdString}.{StackId}.{Key} ({Milliseconds} ms)")]
public sealed class Tracee : ITracee
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private readonly ILogger _logger;

    private readonly string _loggerCategoryName;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Tracee? _parent;

    private readonly ConcurrentStack<Tracee> _stack;
    private readonly ConcurrentDictionary<int, ConcurrentStack<Tracee>> _stackPool;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly ConcurrentDictionary<(int StackId, string Key, int Depth), long> _synced = new();
    private bool _disposed;

    private Tracee(
        string key,
        string? loggerCategoryName,
        ILoggerFactory loggerFactory,
        ConcurrentDictionary<int, ConcurrentStack<Tracee>> stackPool,
        Tracee? parent)
    {
        StackId = CurrentStackId;

        Key = key;
        _loggerCategoryName = loggerCategoryName ?? $"Tracee.{_instanceId:N}";
        _loggerFactory = loggerFactory;
        _stackPool = stackPool;
        _parent = parent;

        Depth = (_parent?.Depth ?? 0) + 1;
        _stack = _stackPool.AddOrUpdate(
            StackId,
            _ => [],
            (_, stack) => stack);
        _logger = _loggerFactory.CreateLogger(_loggerCategoryName);
        _logger.LogTrace(
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

    public int StackId { get; }
    public string Key { get; }
    public int Depth { get; }

    public long Milliseconds => _stopwatch.ElapsedMilliseconds;

    public ITracee Scope(
        string? key = null,
        string memberName = "")
    {
        if (StackToAttach.TryPeek(out var peek) && peek._instanceId != _instanceId)
            return peek.Scope(key, memberName);

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
            _loggerCategoryName,
            _loggerFactory,
            new ConcurrentDictionary<int, ConcurrentStack<Tracee>>(
                [new KeyValuePair<int, ConcurrentStack<Tracee>>(0, [])]),
            this);

        return tracee;
    }

    public IReadOnlyDictionary<ITraceeMetricKey, ITraceeMetricValue> Collect()
    {
        return _synced
            .OrderBy(e => e.Key.Key)
            .ToDictionary<KeyValuePair<(int StackId, string Key, int Depth), long>, ITraceeMetricKey,
                ITraceeMetricValue>(
                item => new TraceeMetricKey(item.Key.StackId, item.Key.Key, item.Key.Depth),
                item => new TraceeMetricValue(item.Value));
    }

    public void Log(LogLevel logLevel, string message)
    {
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _logger.Log(logLevel, message);
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
                (StackId, Key, Depth),
                key => SyncCreateValue(
                    key,
                    _stopwatch.ElapsedMilliseconds),
                (key, value) => SyncUpdateValue(
                    key,
                    value,
                    value + _stopwatch.ElapsedMilliseconds));

            _logger.LogTrace(
                "[disposed {Milliseconds} ms]\t{InstanceId:N}.{Stack}.{Key}",
                _stopwatch.ElapsedMilliseconds,
                _instanceId,
                StackId,
                Key);
        }

        _disposed = true;
    }

    public static ITracee Create(
        string key,
        string? loggerCategoryName,
        ILoggerFactory loggerFactory)
    {
        return new Tracee(
            key,
            loggerCategoryName,
            loggerFactory,
            new ConcurrentDictionary<int, ConcurrentStack<Tracee>>(
                [new KeyValuePair<int, ConcurrentStack<Tracee>>(0, [])]),
            null);
    }

    private long SyncCreateValue((int StackId, string Key, int Depth) key, long value)
    {
        _logger.LogTrace(
            "[sync created] {Stack}.{Key}\t({Milliseconds} ms)",
            key.StackId,
            key.Key,
            value);
        return value;
    }

    private long SyncUpdateValue((int StackId, string Key, int Depth) key, long currentValue, long newValue)
    {
        _logger.LogTrace(
            "[sync updated] {Stack}.{Key}\t({Milliseconds} ms) -> ({NewMilliseconds} ms)",
            key.StackId,
            key.Key,
            currentValue,
            newValue);
        return newValue;
    }
}