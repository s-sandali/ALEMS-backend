using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Endpoints for retrieving algorithms supported by the platform.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT.
/// </remarks>
[ApiController]
[Route("api/algorithms")]
[Authorize]
[Produces("application/json")]
public class AlgorithmController : ControllerBase
{
    private readonly IAlgorithmService _algorithmService;
    private readonly ILogger<AlgorithmController> _logger;

    public AlgorithmController(IAlgorithmService algorithmService, ILogger<AlgorithmController> logger)
    {
        _algorithmService = algorithmService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/algorithms — Retrieve all algorithms.
    /// </summary>
    /// <response code="200">List of all algorithms.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAllAlgorithms()
    {
        try
        {
            var algorithms = await _algorithmService.GetAllAlgorithmsAsync();
            return Ok(new
            {
                status = "success",
                data = algorithms
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/algorithms");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving algorithms.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/algorithms/{id} — Retrieve a single algorithm by ID.
    /// </summary>
    /// <param name="id">The auto-increment algorithm ID.</param>
    /// <response code="200">Algorithm found.</response>
    /// <response code="404">No algorithm with the supplied ID.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAlgorithmById(int id)
    {
        try
        {
            var algorithm = await _algorithmService.GetAlgorithmByIdAsync(id);

            if (algorithm is null)
            {
                return NotFound(new
                {
                    status = "error",
                    message = $"Algorithm with ID {id} not found."
                });
            }

            return Ok(new
            {
                status = "success",
                data = algorithm
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in GET /api/algorithms/{id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving the algorithm.",
                detail = ex.Message
            });
        }
    }
}
