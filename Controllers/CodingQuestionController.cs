using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only endpoints for coding question management (CRUD).
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT with the <b>Admin</b> role.
///
/// **Validation** is enforced by Data Annotations on each request DTO.
/// Invalid payloads are rejected with <c>400 Validation Failed</c> before
/// the action body runs (via <c>InvalidModelStateResponseFactory</c>).
///
/// **Unexpected errors** bubble to <c>GlobalExceptionMiddleware</c> which
/// returns <c>{ statusCode, message, correlationId, traceId }</c> — raw
/// exception details are never exposed to the client.
/// </remarks>
[ApiController]
[Route("api/coding-questions")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class CodingQuestionController : ControllerBase
{
    private readonly ICodingQuestionService _service;
    private readonly ILogger<CodingQuestionController> _logger;

    public CodingQuestionController(ICodingQuestionService service, ILogger<CodingQuestionController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    /// <summary>
    /// GET /api/coding-questions — Retrieve all coding questions.
    /// </summary>
    /// <response code="200">List of all coding questions.</response>
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
    /// GET /api/coding-questions/{id} — Retrieve a coding question by ID.
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

    /// <summary>
    /// POST /api/coding-questions — Create a new coding question.
    /// </summary>
    /// <remarks>
    /// **Validated fields**: <c>title</c> (3–255 chars), <c>description</c> (required),
    /// <c>difficulty</c> (easy | medium | hard).
    /// </remarks>
    /// <response code="201">Coding question created successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateCodingQuestionDto dto)
    {
        var result = await _service.CreateAsync(dto);

        _logger.LogInformation("POST /api/coding-questions — created Id={Id}", result.Id);
        return StatusCode(StatusCodes.Status201Created, new
        {
            status  = "success",
            message = "Coding question created successfully.",
            data    = result
        });
    }

    /// <summary>
    /// PUT /api/coding-questions/{id} — Update an existing coding question.
    /// </summary>
    /// <param name="id">The auto-increment coding question ID.</param>
    /// <param name="dto">Fields to update.</param>
    /// <response code="200">Coding question updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">No coding question with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCodingQuestionDto dto)
    {
        // KeyNotFoundException → 404 via GlobalExceptionMiddleware
        var result = await _service.UpdateAsync(id, dto);

        _logger.LogInformation("PUT /api/coding-questions/{Id} — updated", id);
        return Ok(new
        {
            status  = "success",
            message = "Coding question updated successfully.",
            data    = result
        });
    }

    /// <summary>
    /// DELETE /api/coding-questions/{id} — Delete a coding question.
    /// </summary>
    /// <param name="id">The auto-increment coding question ID.</param>
    /// <response code="204">Coding question deleted successfully.</response>
    /// <response code="404">No coding question with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);

        if (!success)
        {
            return NotFound(new
            {
                status  = "error",
                message = $"Coding question with ID {id} not found."
            });
        }

        return NoContent(); // 204
    }
}
