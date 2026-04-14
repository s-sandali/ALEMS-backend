using System.Net;
using System.Text.Json;
using backend.Services;

namespace backend.Middleware;

/// <summary>
/// Catches all unhandled exceptions across the HTTP pipeline and maps them to
/// appropriate HTTP status codes. Full exception details are logged internally
/// but never exposed to the client.
///
/// Mapping:
///   KeyNotFoundException        → 404
///   ArgumentException           → 400
///   UnauthorizedAccessException → 401
///   NotSupportedException       → 501
///   Judge0RateLimitException    → 429
///   Judge0UnavailableException  → 503
///   OperationCanceledException  → 400  (no error log — client disconnected)
///   Everything else             → 500
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected — not an application error, no log noise
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString()
                                ?? context.TraceIdentifier;

            var (statusCode, logLevel) = ClassifyException(ex);

            _logger.Log(
                logLevel,
                ex,
                "Unhandled {ExceptionType} — CorrelationId={CorrelationId} Method={Method} Path={Path}",
                ex.GetType().Name,
                correlationId,
                context.Request.Method,
                context.Request.Path);

            await WriteErrorResponseAsync(context, ex, statusCode, correlationId);
        }
    }

    // ── Exception → (HTTP status code, log level) ────────────────────────────
    private static (int StatusCode, LogLevel LogLevel) ClassifyException(Exception exception) =>
        exception switch
        {
            KeyNotFoundException        => (StatusCodes.Status404NotFound,            LogLevel.Warning),
            ArgumentException           => (StatusCodes.Status400BadRequest,          LogLevel.Warning),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,        LogLevel.Warning),
            NotSupportedException       => (StatusCodes.Status501NotImplemented,      LogLevel.Warning),
            Judge0RateLimitException    => (StatusCodes.Status429TooManyRequests,     LogLevel.Warning),
            Judge0UnavailableException  => (StatusCodes.Status503ServiceUnavailable,  LogLevel.Error),
            _                           => (StatusCodes.Status500InternalServerError, LogLevel.Error)
        };

    // ── Write standardised JSON error response ────────────────────────────────
    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        Exception   exception,
        int         statusCode,
        string      correlationId)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";

        // Only hide the message for unexpected 500 errors — never leak internal details.
        // All other codes (400, 401, 404, 429, 501, 503) use the exception's own message
        // because it was deliberately composed by service/domain code and is safe to expose.
        var message = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred. Please try again later."
            : exception.Message;

        var body = new
        {
            status = "error",
            statusCode,
            message,
            correlationId,
            traceId = context.TraceIdentifier
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
