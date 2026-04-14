using System.Diagnostics;
using System.Security.Claims;
using Serilog.Context;

namespace backend.Middleware;

/// <summary>
/// Logs every HTTP request with method, path, status code, duration, and user identity.
/// Pushes UserId into the Serilog LogContext so downstream logs are correlated per request.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var userId = ResolveUserId(context.User);

        using (LogContext.PushProperty("UserId", userId))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var durationMs = stopwatch.ElapsedMilliseconds;

                if (durationMs > 2000)
                {
                    _logger.LogWarning(
                        "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        durationMs);
                }
                else
                {
                    _logger.LogInformation(
                        "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms",
                        context.Request.Method,
                        context.Request.Path,
                        context.Response.StatusCode,
                        durationMs);
                }
            }
        }
    }

    private static string ResolveUserId(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "anonymous";
        }

        return user.FindFirstValue("sub") ?? "anonymous";
    }
}
