using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Tracee;

public interface ITracee : IDisposable
{
    long Milliseconds { get; }

    void Log(
        LogLevel logLevel,
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0);

    ITracee Scope(
        string? traceeKey = null,
        [CallerMemberName] string memberName = "");

    ITracee Fixed(string traceeKey);

    void LogAll(LogLevel logLevel);

    void LogSynced(LogLevel logLevel);
}