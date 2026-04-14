using Serilog.Context;

namespace backend.Middleware;

/// <summary>
/// Generates or reads a correlation ID for every HTTP request.
/// The ID is stored in HttpContext.Items, echoed back in the response header,
/// and pushed into the Serilog LogContext so every log entry includes it.
/// A default anonymous UserId is also seeded until authentication-aware middleware enriches it.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Read from incoming header or generate a new one
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString();

        // 2. Store in HttpContext.Items so controllers / services can access it
        context.Items["CorrelationId"] = correlationId;

        // 3. Echo back in the response header for client-side tracing
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // 4. Push baseline request properties so every log entry is enriched automatically
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("UserId", "anonymous"))
        {
            await _next(context);
        }
    }
}
