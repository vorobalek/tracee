using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Tracee;

public interface ITracee : ITraceeMetricKey, ITraceeMetricValue, IDisposable
{
    ITracee Scope(
        string? key = null,
        [CallerMemberName] string memberName = "");

    ITracee Fixed(string key);

    IReadOnlyDictionary<ITraceeMetricKey, ITraceeMetricValue> Collect();

    void Log(LogLevel logLevel, string message);
}