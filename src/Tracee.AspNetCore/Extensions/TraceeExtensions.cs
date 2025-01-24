using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tracee.AspNetCore.Middleware;
using Tracee.AspNetCore.Options;

namespace Tracee.AspNetCore.Extensions;

public static class TraceeExtensions
{
    public static IServiceCollection AddTracee(
        this IServiceCollection services,
        string traceeKey = "",
        string traceeKeySplit = "_",
        bool ignoreNested = false,
        string? loggerCategoryName = null)
    {
        services.AddOptions<TraceeOptions>();
        services.AddScoped<ITracee>(serviceProvider =>
            Tracee.Create(
                traceeKey,
                serviceProvider.GetRequiredService<ILoggerFactory>(),
                traceeKeySplit,
                ignoreNested,
                loggerCategoryName));
        services.AddTransient<TraceeMiddleware>();
        return services;
    }

    public static IApplicationBuilder UseTracee(
        this IApplicationBuilder builder,
        string key = "request",
        Func<ITracee, Task>? preRequestAsync = null,
        Func<ITracee, Task>? postRequestAsync = null)
    {
        var options = builder.ApplicationServices.GetRequiredService<IOptions<TraceeOptions>>();
        options.Value.Key = key;
        options.Value.PreRequestAsync = preRequestAsync;
        options.Value.PostRequestAsync = postRequestAsync;
        return builder.UseMiddleware<TraceeMiddleware>();
    }
}