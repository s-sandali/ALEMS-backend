using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Student-facing read-only endpoints for coding questions.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT (any authenticated user — Student or Admin).
/// Reuses <see cref="ICodingQuestionService"/> — no separate student service needed.
/// </remarks>
[ApiController]
[Route("api/student/coding-questions")]
[Authorize]
[Produces("application/json")]
public class StudentCodingQuestionController : ControllerBase
{
    private readonly ICodingQuestionService                    _service;
    private readonly ILogger<StudentCodingQuestionController> _logger;

    public StudentCodingQuestionController(
        ICodingQuestionService service,
        ILogger<StudentCodingQuestionController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>
    /// GET /api/student/coding-questions — Retrieve all coding questions.
    /// </summary>
    /// <response code="200">List of coding questions.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll()
    {
        var questions = await _service.GetAllAsync();
        return Ok(new
        {
            status = "success",
            data   = questions
        });
    }

    /// <summary>
    /// GET /api/student/coding-questions/{id} — Retrieve a single coding question by ID.
    /// </summary>
    /// <param name="id">The auto-increment coding question ID.</param>
    /// <response code="200">Coding question found.</response>
    /// <response code="404">No coding question with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id)
    {
        var question = await _service.GetByIdAsync(id);
        if (question is null)
        {
            return NotFound(new
            {
                status  = "error",
                message = $"Coding question with ID {id} not found."
            });
        }

        return Ok(new
        {
            status = "success",
            data   = question
        });
    }
}
