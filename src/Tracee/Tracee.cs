using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Tracee;

public sealed class Tracee : ITracee
{
    private readonly Tracee? _parent;
    private readonly Guid _instanceId = Guid.NewGuid();
    
    private bool _disposed;
    private readonly string _traceeKey;
    private readonly ILogger _logger;
    private readonly string _loggerCategoryName;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentStack<Tracee> _stack;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly ConcurrentDictionary<string, long> _synced = new();

    public static ITracee Create(
        string traceeKey,
        string loggerCategoryName,
        ILoggerFactory loggerFactory)
    {
        return new Tracee(traceeKey, loggerCategoryName, loggerFactory, [], null);
    }

    private Tracee(
        string traceeKey,
        string loggerCategoryName,
        ILoggerFactory loggerFactory,
        ConcurrentStack<Tracee> stack,
        Tracee? parent)
    {
        _traceeKey = traceeKey;
        _loggerCategoryName = loggerCategoryName;
        _loggerFactory = loggerFactory;
        _stack = stack;
        _logger = _loggerFactory.CreateLogger(loggerCategoryName);
        _parent = parent;
        _logger.LogTrace(
            "[created {Milliseconds} ms] {InstanceId:N} {Key}",
            _stopwatch.ElapsedMilliseconds, 
            _instanceId,
            _traceeKey);
    }

    public long Milliseconds => _stopwatch.ElapsedMilliseconds;

    public void Log(
        LogLevel logLevel,
        string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
    {
        var value = _stopwatch.ElapsedMilliseconds;
        _logger.Log(
            logLevel,
            "[{Milliseconds} ms] {InstanceId:N} {Key}\n{Message}\n{MemberName}\n{SourceFilePath}:{SourceLineNumber}", 
            value, 
            _instanceId,
            _traceeKey,
            message,
            memberName,
            sourceFilePath,
            sourceLineNumber);
    }

    public ITracee Scope(
        string? traceeKey = null,
        string memberName = "")
    {
        if (_stack.TryPeek(out var peek) && peek._instanceId != _instanceId)
            return peek.Scope(traceeKey, memberName);

        traceeKey = traceeKey switch
        {
            null => memberName switch
            {
                _ when !string.IsNullOrWhiteSpace(memberName) => $"{_traceeKey}_memberName",
                _ => $"{_traceeKey}_{Guid.NewGuid():N}"
            },
            _ => $"{_traceeKey}_{traceeKey}"
        };

        var tracee = new Tracee(
            traceeKey, 
            _loggerCategoryName,
            _loggerFactory, 
            _stack, 
            this);
        _stack.Push(tracee);

        return tracee;
    }

    public ITracee Fixed(string traceeKey)
    {
        if (_parent != null)
            return _parent.Fixed(traceeKey);

        var tracee = new Tracee(
            traceeKey, 
            _loggerCategoryName,
            _loggerFactory, 
            [], 
            this);

        return tracee;
    }

    public void LogAll(LogLevel logLevel)
    {
        var orderedMetrics = _synced
            .Concat([new KeyValuePair<string, long>(_traceeKey, _stopwatch.ElapsedMilliseconds)])
            .OrderBy(e => e.Key)
            .Select(e => (e.Key, e.Value));

        var content = BuildPrettyLog(orderedMetrics);
        
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _logger.Log(logLevel, content);
    }

    public void LogSynced(LogLevel logLevel)
    {
        var orderedMetrics = _synced
            .OrderBy(e => e.Key)
            .Select(e => (e.Key, e.Value));

        var content = BuildPrettyLog(orderedMetrics);

        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        _logger.Log(logLevel, content);
    }

    private static string BuildPrettyLog(IEnumerable<(string Key, long Value)> metrics)
    {
        var prepared = metrics.Select(e => (e.Key, Value: e.Value.ToString())).ToArray();
        var padding = prepared.Max(e => e.Value.Length);
        var stringBuilder = new StringBuilder();
        foreach (var (key, value) in prepared)
        {
            stringBuilder.AppendLine($"[{value.PadLeft(padding)} ms] {key}");
        }
        return stringBuilder.ToString();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            while (_stack.TryPop(out var tracee) && tracee._instanceId != _instanceId)
            {
                tracee.Dispose();
            }

            _stopwatch.Stop();
            foreach (var metrics in _synced)
            {
                _parent?._synced.AddOrUpdate(
                    metrics.Key,
                    _ => metrics.Value,
                    (_, v) => v + metrics.Value);
            }
            _parent?._synced.AddOrUpdate(
                _traceeKey,
                _ => _stopwatch.ElapsedMilliseconds,
                (_, v) => v + _stopwatch.ElapsedMilliseconds);
            if (_parent != null)
                _logger.LogTrace(
                    "[synced] {InstanceId:N} {Key} -> {ParentInstanceId:N} {ParentKey}", 
                    _traceeKey,
                    _instanceId,
                    _parent._traceeKey,
                    _parent._instanceId);
            _logger.LogTrace(
                "[disposed {Milliseconds} ms] {InstanceId:N} {Key}", 
                _stopwatch.ElapsedMilliseconds,
                _instanceId,
                _traceeKey);
        }
        _disposed = true;
    }
}