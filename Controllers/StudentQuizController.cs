using System.Security.Claims;
using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Student-facing endpoints for the quiz system — browse, fetch questions, and submit attempts.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT (any authenticated user — Student or Admin).
/// Only <b>active</b> quizzes and questions are returned from read endpoints.
/// <b>Correct answers are never included in question responses.</b>
/// </remarks>
[ApiController]
[Route("api/student")]
[Authorize]
[Produces("application/json")]
public class StudentQuizController : ControllerBase
{
    private readonly IQuizService            _quizService;
    private readonly IQuizQuestionService    _questionService;
    private readonly IQuizAttemptService     _attemptService;
    private readonly ILogger<StudentQuizController> _logger;

    public StudentQuizController(
        IQuizService quizService,
        IQuizQuestionService questionService,
        IQuizAttemptService attemptService,
        ILogger<StudentQuizController> logger)
    {
        _quizService     = quizService;
        _questionService = questionService;
        _attemptService  = attemptService;
        _logger          = logger;
    }

    // ── GET /api/student/quizzes ─────────────────────────────────────

    /// <summary>
    /// GET /api/student/quizzes — Retrieve all active quizzes.
    /// </summary>
    /// <remarks>Returns only quizzes where <c>is_active = true</c>.</remarks>
    /// <response code="200">List of active quizzes.</response>
    [HttpGet("quizzes")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveQuizzes()
    {
        var quizzes = await _quizService.GetActiveQuizzesAsync();
        return Ok(new { status = "success", data = quizzes });
    }

    // ── GET /api/student/quizzes/{id} ────────────────────────────────

    /// <summary>
    /// GET /api/student/quizzes/{id} — Retrieve an active quiz by ID.
    /// </summary>
    /// <param name="id">The quiz ID.</param>
    /// <response code="200">Quiz found and active.</response>
    /// <response code="404">Quiz not found or inactive.</response>
    [HttpGet("quizzes/{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveQuizById(int id)
    {
        var quiz = await _quizService.GetActiveQuizByIdAsync(id);
        if (quiz is null)
            return NotFound(new { status = "error", message = $"Quiz with ID {id} not found." });

        return Ok(new { status = "success", data = quiz });
    }

    // ── GET /api/student/quizzes/{quizId}/questions ──────────────────

    /// <summary>
    /// GET /api/student/quizzes/{quizId}/questions — Retrieve questions for an active quiz.
    /// </summary>
    /// <param name="quizId">The quiz ID.</param>
    /// <remarks>
    /// Returns only active questions ordered by <c>order_index</c>.
    /// <b>Correct answers and explanations are not included.</b>
    /// </remarks>
    /// <response code="200">List of questions (without correct answers).</response>
    /// <response code="404">Quiz not found or inactive.</response>
    [HttpGet("quizzes/{quizId:int}/questions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveQuestions(int quizId)
    {
        try
        {
            var questions = await _questionService.GetActiveQuestionsForStudentAsync(quizId);
            return Ok(new { status = "success", data = questions });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new { status = "error", message = knfe.Message });
        }
    }

    // ── POST /api/student/quizzes/{quizId}/attempt ───────────────────

    /// <summary>
    /// POST /api/student/quizzes/{quizId}/attempt — Submit answers and receive a graded result.
    /// </summary>
    /// <param name="quizId">The quiz ID to attempt.</param>
    /// <param name="dto">Answer collection — one entry per active question.</param>
    /// <remarks>
    /// Grades all answers, persists the attempt, awards XP for correct answers,
    /// and returns per-question feedback including explanations.
    /// </remarks>
    /// <response code="200">Attempt graded successfully. Returns score, XP, and per-question results.</response>
    /// <response code="400">Validation failed or submitted answers are invalid.</response>
    /// <response code="404">Quiz not found or user not yet synced.</response>
    [HttpPost("quizzes/{quizId:int}/attempt")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAttempt(int quizId, [FromBody] CreateQuizAttemptDto dto)
    {
        var clerkUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(clerkUserId))
            return Unauthorized(new { status = "error", message = "Invalid token: missing user identifier." });

        try
        {
            var result = await _attemptService.SubmitAttemptAsync(quizId, clerkUserId, dto);

            _logger.LogInformation(
                "POST /api/student/quizzes/{QuizId}/attempt — submitted for ClerkId={ClerkId}",
                quizId, clerkUserId);

            return Ok(new
            {
                status  = "success",
                message = "Quiz attempt submitted successfully.",
                data    = result
            });
        }
        catch (ArgumentException ae)
        {
            return BadRequest(new { status = "error", message = ae.Message });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new { status = "error", message = knfe.Message });
        }
    }
}
