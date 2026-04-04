using System.Security.Claims;
using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only endpoints for quiz management (CRUD).
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT with the <b>Admin</b> role.
///
/// **Validation** is enforced by Data Annotations on each request DTO.
/// Invalid payloads are rejected with <c>400 Validation Failed</c> before
/// the action body runs (via <c>InvalidModelStateResponseFactory</c>).
///
/// **Unexpected errors** bubble to <c>GlobalExceptionMiddleware</c> which
/// returns <c>{ statusCode, message, traceId }</c> — raw exception details
/// are never exposed to the client.
/// </remarks>
[ApiController]
[Route("api/quizzes")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class QuizController : ControllerBase
{
    private readonly IQuizService _quizService;
    private readonly ILogger<QuizController> _logger;

    public QuizController(IQuizService quizService, ILogger<QuizController> logger)
    {
        _quizService = quizService;
        _logger      = logger;
    }

    /// <summary>
    /// GET /api/quizzes — Retrieve all quizzes (active and inactive).
    /// </summary>
    /// <response code="200">List of all quizzes.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllQuizzes()
    {
        var quizzes = await _quizService.GetAllQuizzesAsync();
        return Ok(new
        {
            status = "success",
            data   = quizzes
        });
    }

    /// <summary>
    /// GET /api/quizzes/{id} — Retrieve a quiz by ID.
    /// </summary>
    /// <param name="id">The auto-increment quiz ID.</param>
    /// <response code="200">Quiz found.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQuizById(int id)
    {
        var quiz = await _quizService.GetQuizByIdAsync(id);
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

    /// <summary>
    /// POST /api/quizzes — Create a new quiz.
    /// </summary>
    /// <remarks>
    /// The <c>created_by</c> field is resolved automatically from the caller's Clerk JWT.
    ///
    /// **Validated fields**: <c>algorithmId</c> (required, &gt;0), <c>title</c> (3–255 chars),
    /// <c>description</c> (max 2000), <c>timeLimitMins</c> (1–300), <c>passScore</c> (0–100).
    /// </remarks>
    /// <response code="201">Quiz created successfully.</response>
    /// <response code="400">Validation failed or referenced algorithm does not exist.</response>
    /// <response code="404">Authenticated user has no local account (sync required).</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizDto dto)
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

        try
        {
            var result = await _quizService.CreateQuizAsync(dto, clerkUserId);

            _logger.LogInformation("POST /api/quizzes — created QuizId={QuizId}", result.QuizId);
            return StatusCode(StatusCodes.Status201Created, new
            {
                status  = "success",
                message = "Quiz created successfully.",
                data    = result
            });
        }
        catch (ArgumentException ae)
        {
            return BadRequest(new
            {
                status  = "error",
                message = ae.Message
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
        // → { statusCode: 500, message: "...", traceId: "..." }
    }

    /// <summary>
    /// PUT /api/quizzes/{id} — Update an existing quiz.
    /// </summary>
    /// <param name="id">The auto-increment quiz ID.</param>
    /// <param name="dto">Fields to update.</param>
    /// <response code="200">Quiz updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateQuiz(int id, [FromBody] UpdateQuizDto dto)
    {
        try
        {
            var result = await _quizService.UpdateQuizAsync(id, dto);

            _logger.LogInformation("PUT /api/quizzes/{Id} — updated", id);
            return Ok(new
            {
                status  = "success",
                message = "Quiz updated successfully.",
                data    = result
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

    /// <summary>
    /// DELETE /api/quizzes/{id} — Soft-delete a quiz (sets is_active = false).
    /// </summary>
    /// <param name="id">The auto-increment quiz ID.</param>
    /// <response code="204">Quiz soft-deleted successfully.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteQuiz(int id)
    {
        var success = await _quizService.DeleteQuizAsync(id);

        if (!success)
        {
            return NotFound(new
            {
                status  = "error",
                message = $"Quiz with ID {id} not found."
            });
        }

        return NoContent(); // 204
        // All other exceptions bubble to GlobalExceptionMiddleware
    }
}
