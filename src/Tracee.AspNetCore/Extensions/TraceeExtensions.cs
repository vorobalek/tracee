using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tracee.AspNetCore.Middleware;

namespace Tracee.AspNetCore.Extensions;

public static class TraceeExtensions
{
    public static IServiceCollection AddTracee(
        this IServiceCollection services,
        string traceeKey = "tracee",
        string? loggerCategoryName = null)
    {
        return services
            .AddScoped<ITracee>(serviceProvider =>
                Tracee.Create(
                    traceeKey,
                    loggerCategoryName ?? $"Tracee.{Guid.NewGuid():N}",
                    serviceProvider.GetRequiredService<ILoggerFactory>()))
            .AddScoped<TraceeMiddleware>();
    }
    
    public static IApplicationBuilder UseTracee(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TraceeMiddleware>();
    }
}