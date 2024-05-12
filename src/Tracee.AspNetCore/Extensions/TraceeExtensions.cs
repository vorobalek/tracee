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
        string? loggerCategoryName = null)
    {
        services.AddOptions<TraceeOptions>();
        services.AddScoped<ITracee>(serviceProvider =>
            Tracee.Create(
                traceeKey,
                loggerCategoryName,
                serviceProvider.GetRequiredService<ILoggerFactory>()));
        services.AddTransient<TraceeMiddleware>();
        return services;
    }

    public static IApplicationBuilder UseTracee(
        this IApplicationBuilder builder,
        string key = "request",
        LogLevel? logLevel = null)
    {
        var options = builder.ApplicationServices.GetRequiredService<IOptions<TraceeOptions>>();
        options.Value.Key = key;
        options.Value.LogLevel = logLevel;
        return builder.UseMiddleware<TraceeMiddleware>();
    }
}