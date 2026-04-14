using System.Security.Claims;
using backend.DTOs;
using backend.Services;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Endpoints for live code execution via Judge0.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT (any authenticated user — Student or Admin).
///
/// **Judge0 mode**: <c>wait=true</c> (synchronous) — one request, no polling, result returned immediately.
///
/// **Unexpected errors** bubble to <c>GlobalExceptionMiddleware</c> which returns
/// <c>{ statusCode, message, correlationId, traceId }</c> — raw exception details are never exposed.
///
/// Judge0-specific exceptions are mapped automatically:
///   <c>Judge0RateLimitException</c>    → 429 Too Many Requests
///   <c>Judge0UnavailableException</c>  → 503 Service Unavailable
/// </remarks>
[ApiController]
[Route("api/code")]
[Authorize]
[Produces("application/json")]
public class CodeExecutionController : ControllerBase
{
    private readonly ICodeExecutionService _executionService;
    private readonly ILogger<CodeExecutionController> _logger;
    private readonly TelemetryClient _telemetryClient;

    public CodeExecutionController(
        ICodeExecutionService executionService,
        ILogger<CodeExecutionController> logger,
        TelemetryClient telemetryClient)
    {
        _executionService = executionService;
        _logger           = logger;
        _telemetryClient  = telemetryClient;
    }

    /// <summary>
    /// GET /api/code/languages — Retrieve the list of supported languages.
    /// </summary>
    /// <response code="200">List of supported languages with their Judge0 IDs.</response>
    [HttpGet("languages")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetLanguages()
    {
        var languages = _executionService.GetSupportedLanguages();
        return Ok(new
        {
            status = "success",
            data   = languages
        });
    }

    /// <summary>
    /// POST /api/code/execute — Execute source code via Judge0.
    /// </summary>
    /// <remarks>
    /// Submits code synchronously (<c>wait=true</c>). The server blocks until Judge0 returns
    /// a result (typically &lt;5 seconds). The response includes stdout, stderr, compile output,
    /// and a status descriptor.
    ///
    /// **Status IDs**: 3 = Accepted, 4 = Wrong Answer, 5 = Time Limit Exceeded,
    /// 6 = Compilation Error, 7–12 = Runtime Error variants.
    /// </remarks>
    /// <response code="200">Execution completed (check <c>statusId</c> for outcome).</response>
    /// <response code="400">Validation failed or unsupported language ID.</response>
    /// <response code="429">Daily execution quota exhausted.</response>
    /// <response code="503">Judge0 service unavailable or timed out.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Execute(
        [FromBody] CodeExecutionRequestDto dto,
        CancellationToken ct)
    {
        // ArgumentException           → 400 via GlobalExceptionMiddleware
        // Judge0RateLimitException    → 429 via GlobalExceptionMiddleware
        // Judge0UnavailableException  → 503 via GlobalExceptionMiddleware
        // OperationCanceledException  → 400 (client disconnected, no error log)
        var result = await _executionService.ExecuteAsync(dto, ct);
        var clerkUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "anonymous";

        _telemetryClient.TrackEvent(
            "CodeExecuted",
            new Dictionary<string, string>
            {
                ["userId"] = clerkUserId,
                ["language"] = dto.LanguageId.ToString(),
                ["status"] = string.IsNullOrWhiteSpace(result.StatusDescription)
                    ? result.StatusId.ToString()
                    : result.StatusDescription,
                ["correlationId"] = ResolveCorrelationId()
            });

        _logger.LogInformation(
            "POST /api/code/execute — LanguageId={LanguageId} StatusId={StatusId}",
            dto.LanguageId, result.StatusId);

        return Ok(new
        {
            status = "success",
            data   = result
        });
    }

    private string ResolveCorrelationId()
    {
        return HttpContext.Items["CorrelationId"]?.ToString() ?? HttpContext.TraceIdentifier;
    }
}
