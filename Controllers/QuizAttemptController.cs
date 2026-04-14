using System.Security.Claims;
using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Authenticated endpoints for quiz attempt submission.
/// </summary>
/// <remarks>
/// Requires a valid Clerk JWT.
///
/// **Validation** is enforced by Data Annotations on the request DTO.
/// Invalid payloads are rejected with <c>400 Validation Failed</c> before
/// the action body runs (via <c>InvalidModelStateResponseFactory</c>).
///
/// **Unexpected errors** bubble to <c>GlobalExceptionMiddleware</c> which
/// returns <c>{ statusCode, message, correlationId, traceId }</c>.
/// </remarks>
[ApiController]
[Route("api/quizzes/{quizId:int}/attempts")]
[Authorize]
[Produces("application/json")]
public class QuizAttemptController : ControllerBase
{
    private readonly IQuizAttemptService _attemptService;
    private readonly ILogger<QuizAttemptController> _logger;

    public QuizAttemptController(
        IQuizAttemptService attemptService,
        ILogger<QuizAttemptController> logger)
    {
        _attemptService = attemptService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/quizzes/{quizId}/attempts — Submit a student's answers for grading.
    /// </summary>
    /// <param name="quizId">The target quiz ID.</param>
    /// <param name="dto">Submitted answer collection.</param>
    /// <response code="200">Attempt recorded successfully.</response>
    /// <response code="400">Validation failed or submitted answers are invalid.</response>
    /// <response code="404">Quiz or local user record not found.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SubmitAttempt(int quizId, [FromBody] CreateQuizAttemptDto dto)
    {
        var clerkUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            return Unauthorized(new
            {
                status  = "error",
                message = "Invalid token: missing user identifier."
            });
        }

        // ArgumentException (invalid answers) → 400 via GlobalExceptionMiddleware
        // KeyNotFoundException (quiz/user)    → 404 via GlobalExceptionMiddleware
        var result = await _attemptService.SubmitAttemptAsync(quizId, clerkUserId, dto);

        _logger.LogInformation(
            "POST /api/quizzes/{QuizId}/attempts — attempt submitted for ClerkId={ClerkId}",
            quizId,
            clerkUserId);

        return Ok(new
        {
            status  = "success",
            message = "Quiz attempt submitted successfully.",
            data    = result
        });
    }
}
