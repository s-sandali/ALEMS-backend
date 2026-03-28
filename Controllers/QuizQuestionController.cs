using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only endpoints for managing questions within a quiz.
/// Supports both MCQ and PREDICT_STEP question types —
/// both are stored identically; only <c>question_type</c> differs.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT with the <b>Admin</b> role.
/// </remarks>
[ApiController]
[Route("api/quizzes/{quizId:int}/questions")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class QuizQuestionController : ControllerBase
{
    private readonly IQuizQuestionService _questionService;
    private readonly ILogger<QuizQuestionController> _logger;

    public QuizQuestionController(
        IQuizQuestionService questionService,
        ILogger<QuizQuestionController> logger)
    {
        _questionService = questionService;
        _logger          = logger;
    }

    // ── GET /api/quizzes/{quizId}/questions ──────────────────────────

    /// <summary>
    /// GET /api/quizzes/{quizId}/questions — Retrieve all active questions for a quiz.
    /// </summary>
    /// <param name="quizId">The parent quiz ID.</param>
    /// <response code="200">Ordered list of active questions.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQuestions(int quizId)
    {
        try
        {
            var questions = await _questionService.GetByQuizIdAsync(quizId);
            return Ok(new
            {
                status = "success",
                data   = questions
            });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new { status = "error", message = knfe.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/quizzes/{QuizId}/questions", quizId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while retrieving questions.",
                detail  = ex.Message
            });
        }
    }

    // ── GET /api/quizzes/{quizId}/questions/{id} ─────────────────────

    /// <summary>
    /// GET /api/quizzes/{quizId}/questions/{id} — Retrieve a single question by ID.
    /// </summary>
    /// <param name="quizId">The parent quiz ID.</param>
    /// <param name="id">The question ID.</param>
    /// <response code="200">Question found.</response>
    /// <response code="404">No question with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQuestion(int quizId, int id)
    {
        try
        {
            var question = await _questionService.GetByIdAsync(id);
            if (question is null || question.QuizId != quizId)
            {
                return NotFound(new
                {
                    status  = "error",
                    message = $"Question with ID {id} not found in quiz {quizId}."
                });
            }

            return Ok(new { status = "success", data = question });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error in GET /api/quizzes/{QuizId}/questions/{Id}", quizId, id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while retrieving the question.",
                detail  = ex.Message
            });
        }
    }

    // ── POST /api/quizzes/{quizId}/questions ─────────────────────────

    /// <summary>
    /// POST /api/quizzes/{quizId}/questions — Add a new question to a quiz.
    /// </summary>
    /// <remarks>
    /// Set <c>question_type</c> to <c>"PREDICT_STEP"</c> for algorithm-step prediction questions;
    /// use <c>"MCQ"</c> for standard knowledge checks.
    /// Both types share the same four-option, single-answer structure.
    /// </remarks>
    /// <param name="quizId">The parent quiz ID.</param>
    /// <param name="dto">Question data.</param>
    /// <response code="201">Question created successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQuestion(int quizId, [FromBody] CreateQuizQuestionDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    status  = "error",
                    message = "Validation failed.",
                    errors  = ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray())
                });
            }

            var result = await _questionService.CreateAsync(quizId, dto);

            _logger.LogInformation(
                "POST /api/quizzes/{QuizId}/questions — created QuestionId={QuestionId}",
                quizId, result.QuestionId);

            return StatusCode(StatusCodes.Status201Created, new
            {
                status  = "success",
                message = "Question created successfully.",
                data    = result
            });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new { status = "error", message = knfe.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error in POST /api/quizzes/{QuizId}/questions", quizId);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while creating the question.",
                detail  = ex.Message
            });
        }
    }

    // ── PUT /api/quizzes/{quizId}/questions/{id} ─────────────────────

    /// <summary>
    /// PUT /api/quizzes/{quizId}/questions/{id} — Update an existing question.
    /// </summary>
    /// <param name="quizId">The parent quiz ID.</param>
    /// <param name="id">The question ID.</param>
    /// <param name="dto">Fields to update.</param>
    /// <response code="200">Question updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No question with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateQuestion(int quizId, int id, [FromBody] UpdateQuizQuestionDto dto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    status  = "error",
                    message = "Validation failed.",
                    errors  = ModelState
                        .Where(e => e.Value?.Errors.Count > 0)
                        .ToDictionary(
                            e => e.Key,
                            e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray())
                });
            }

            // Verify the question belongs to the route's quiz
            var existing = await _questionService.GetByIdAsync(id);
            if (existing is null || existing.QuizId != quizId)
            {
                return NotFound(new
                {
                    status  = "error",
                    message = $"Question with ID {id} not found in quiz {quizId}."
                });
            }

            var result = await _questionService.UpdateAsync(id, dto);

            _logger.LogInformation(
                "PUT /api/quizzes/{QuizId}/questions/{Id} — updated", quizId, id);

            return Ok(new
            {
                status  = "success",
                message = "Question updated successfully.",
                data    = result
            });
        }
        catch (KeyNotFoundException knfe)
        {
            return NotFound(new { status = "error", message = knfe.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error in PUT /api/quizzes/{QuizId}/questions/{Id}", quizId, id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while updating the question.",
                detail  = ex.Message
            });
        }
    }

    // ── DELETE /api/quizzes/{quizId}/questions/{id} ──────────────────

    /// <summary>
    /// DELETE /api/quizzes/{quizId}/questions/{id} — Soft-delete a question (sets is_active = false).
    /// </summary>
    /// <param name="quizId">The parent quiz ID.</param>
    /// <param name="id">The question ID.</param>
    /// <response code="204">Question soft-deleted successfully.</response>
    /// <response code="404">No question with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteQuestion(int quizId, int id)
    {
        try
        {
            // Verify ownership before deleting
            var existing = await _questionService.GetByIdAsync(id);
            if (existing is null || existing.QuizId != quizId)
            {
                return NotFound(new
                {
                    status  = "error",
                    message = $"Question with ID {id} not found in quiz {quizId}."
                });
            }

            await _questionService.DeleteAsync(id);
            return NoContent(); // 204
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled error in DELETE /api/quizzes/{QuizId}/questions/{Id}", quizId, id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while deleting the question.",
                detail  = ex.Message
            });
        }
    }
}
