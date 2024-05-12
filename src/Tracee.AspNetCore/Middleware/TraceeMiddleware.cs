using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Tracee.AspNetCore.Middleware;

internal sealed class TraceeMiddleware(ITracee tracee) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        using (tracee.Scope("request"))
        {
            await next(context);
        }
        tracee.LogAll(LogLevel.Debug);
    }
}