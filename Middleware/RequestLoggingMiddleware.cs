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

    private const long SlowRequestThresholdMs = 2000;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var userId = ResolveUserId(context.User);

        using (LogContext.PushProperty("UserId", userId))
        {
            try
            {
                await _next(context);
                stopwatch.Stop();

                LogCompleted(context, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception)
            {
                stopwatch.Stop();

                // Status code is unreliable when an exception escapes (response hasn't started).
                // Log the duration only; GlobalExceptionMiddleware owns the error details.
                _logger.LogWarning(
                    "HTTP {Method} {Path} threw exception after {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);

                throw;
            }
        }
    }

    private void LogCompleted(HttpContext context, long durationMs)
    {
        var method     = context.Request.Method;
        var path       = context.Request.Path;
        var statusCode = context.Response.StatusCode;

        if (durationMs > SlowRequestThresholdMs)
            _logger.LogWarning(
                "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms",
                method, path, statusCode, durationMs);
        else
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {Duration}ms",
                method, path, statusCode, durationMs);
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
