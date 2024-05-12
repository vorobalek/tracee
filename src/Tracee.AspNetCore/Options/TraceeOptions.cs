using Microsoft.Extensions.Logging;

namespace Tracee.AspNetCore.Options;

internal record TraceeOptions
{
    public string Key { get; set; } = "request";
    public LogLevel? LogLevel { get; set; }
}