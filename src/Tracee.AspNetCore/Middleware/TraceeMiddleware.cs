using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Tracee.AspNetCore.Options;

namespace Tracee.AspNetCore.Middleware;

internal sealed class TraceeMiddleware(
    ITracee tracee,
    IOptions<TraceeOptions> options) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (options.Value.IgnorePathPrefix is not null && 
            context.Request.GetEncodedPathAndQuery().StartsWith(options.Value.IgnorePathPrefix))
            await next(context);
        else
        {
            if (options.Value.PreRequestAsync is not null)
                await options.Value.PreRequestAsync(tracee);

            using (tracee.Scoped(options.Value.Key))
            {
                await next(context);
            }

            if (options.Value.PostRequestAsync is not null)
                await options.Value.PostRequestAsync(tracee);
        }
    }
}