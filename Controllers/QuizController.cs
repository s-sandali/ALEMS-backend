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
    /// GET /api/quizzes — Retrieve all quizzes.
    /// </summary>
    /// <response code="200">List of all quizzes.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllQuizzes()
    {
        try
        {
            var quizzes = await _quizService.GetAllQuizzesAsync();
            return Ok(new
            {
                status = "success",
                data   = quizzes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/quizzes");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while retrieving quizzes.",
                detail  = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/quizzes/{id} — Retrieve a quiz by ID.
    /// </summary>
    /// <param name="id">The auto-increment quiz ID.</param>
    /// <response code="200">Quiz found.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetQuizById(int id)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/quizzes/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while retrieving the quiz.",
                detail  = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/quizzes — Create a new quiz.
    /// </summary>
    /// <remarks>
    /// The <c>created_by</c> field is resolved automatically from the caller's Clerk JWT.
    /// </remarks>
    /// <response code="201">Quiz created successfully.</response>
    /// <response code="400">Validation failed or referenced algorithm does not exist.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateQuiz([FromBody] CreateQuizDto dto)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in POST /api/quizzes");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while creating the quiz.",
                detail  = ex.Message
            });
        }
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
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateQuiz(int id, [FromBody] UpdateQuizDto dto)
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in PUT /api/quizzes/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while updating the quiz.",
                detail  = ex.Message
            });
        }
    }

    /// <summary>
    /// DELETE /api/quizzes/{id} — Soft-delete a quiz (sets is_active = false).
    /// </summary>
    /// <param name="id">The auto-increment quiz ID.</param>
    /// <response code="204">Quiz soft-deleted successfully.</response>
    /// <response code="404">No quiz with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteQuiz(int id)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in DELETE /api/quizzes/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while deleting the quiz.",
                detail  = ex.Message
            });
        }
    }
}
