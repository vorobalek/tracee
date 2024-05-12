using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Tracee;

public interface ITracee : ITraceeMetricLabels, ITraceeMetricValue, IDisposable
{
    ITracee Scoped(
        string? key = null,
        [CallerMemberName] string memberName = "");

    ITracee Fixed(string key);

    IReadOnlyDictionary<ITraceeMetricLabels, ITraceeMetricValue> Collect();

    void Log(LogLevel logLevel, string message);
}