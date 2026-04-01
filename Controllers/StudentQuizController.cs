using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Student-facing read-only endpoints for the quiz system.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT (any authenticated user — Student or Admin).
/// Only <b>active</b> quizzes and questions are returned.
/// <b>Correct answers are never included in any response from this controller.</b>
/// </remarks>
[ApiController]
[Route("api/student")]
[Authorize]
[Produces("application/json")]
public class StudentQuizController : ControllerBase
{
    private readonly IQuizService         _quizService;
    private readonly IQuizQuestionService _questionService;
    private readonly ILogger<StudentQuizController> _logger;

    public StudentQuizController(
        IQuizService quizService,
        IQuizQuestionService questionService,
        ILogger<StudentQuizController> logger)
    {
        _quizService     = quizService;
        _questionService = questionService;
        _logger          = logger;
    }

    // ── GET /api/student/quizzes ─────────────────────────────────────

    /// <summary>
    /// GET /api/student/quizzes — Retrieve all active quizzes.
    /// </summary>
    /// <remarks>
    /// Returns only quizzes where <c>is_active = true</c>.
    /// Inactive quizzes are not visible to students.
    /// </remarks>
    /// <response code="200">List of active quizzes.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("quizzes")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveQuizzes()
    {
        var quizzes = await _quizService.GetActiveQuizzesAsync();
        return Ok(new
        {
            status = "success",
            data   = quizzes
        });
    }

    // ── GET /api/student/quizzes/{id} ────────────────────────────────

    /// <summary>
    /// GET /api/student/quizzes/{id} — Retrieve an active quiz by ID.
    /// </summary>
    /// <param name="id">The quiz ID.</param>
    /// <remarks>
    /// Returns <c>404</c> if the quiz does not exist <b>or</b> is inactive.
    /// Students cannot discover inactive quizzes via this endpoint.
    /// </remarks>
    /// <response code="200">Quiz found and active.</response>
    /// <response code="404">Quiz not found or inactive.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("quizzes/{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveQuizById(int id)
    {
        var quiz = await _quizService.GetActiveQuizByIdAsync(id);
        if (quiz is null)
        {
            return NotFound(new
            {
                status  = "error",
                message = $"Quiz with ID {id} not found."
            });
        }

        return Ok(new
        {
            status = "success",
            data   = quiz
        });
    }

    // ── GET /api/student/quizzes/{quizId}/questions ──────────────────

    /// <summary>
    /// GET /api/student/quizzes/{quizId}/questions — Retrieve questions for an active quiz.
    /// </summary>
    /// <param name="quizId">The quiz ID.</param>
    /// <remarks>
    /// Returns only active questions ordered by <c>order_index</c>.
    /// <b>Correct answers are not included in the response.</b>
    /// Returns <c>404</c> if the quiz does not exist or is inactive.
    /// </remarks>
    /// <response code="200">List of questions (without correct answers).</response>
    /// <response code="404">Quiz not found or inactive.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("quizzes/{quizId:int}/questions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveQuestions(int quizId)
    {
        try
        {
            var questions = await _questionService.GetActiveQuestionsForStudentAsync(quizId);
            return Ok(new
            {
                status = "success",
                data   = questions
            });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new
            {
                status  = "error",
                message = knfe.Message
            });
        }
        // All other exceptions bubble to GlobalExceptionMiddleware
    }
}
