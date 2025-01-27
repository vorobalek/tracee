namespace Tracee.AspNetCore.Options;

internal record TraceeOptions
{
    public string Key { get; set; } = "request";
    public string? IgnorePathPrefix { get; set; }
    public Func<ITracee, Task>? PreRequestAsync { get; set; }
    public Func<ITracee, Task>? PostRequestAsync { get; set; }
}