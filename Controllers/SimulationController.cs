using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Endpoints for running algorithm simulations.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT.
///
/// **Unexpected errors** bubble to <c>GlobalExceptionMiddleware</c>:
///   <c>NotSupportedException</c>  → 501 Not Implemented
///   <c>KeyNotFoundException</c>   → 404 Not Found
/// </remarks>
[ApiController]
[Route("api/simulation")]
[Authorize]
[Produces("application/json")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulationService;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(
        ISimulationService simulationService,
        ILogger<SimulationController> logger)
    {
        _simulationService = simulationService;
        _logger = logger;
    }

    /// <summary>
    /// GET /simulate/insertion-sort
    /// Compatibility endpoint that runs insertion sort simulation.
    /// If no query values are provided, a sample array is used.
    /// </summary>
    /// <response code="200">Simulation completed.</response>
    /// <response code="501">Insertion sort engine not registered.</response>
    [HttpGet("/simulate/insertion-sort")]
    [ProducesResponseType(typeof(backend.Models.Simulations.SimulationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetInsertionSortSimulation([FromQuery] int[]? array)
    {
        // NotSupportedException → 501 via GlobalExceptionMiddleware
        var input = array is { Length: > 0 } ? array : [5, 2, 4, 6, 1, 3];
        var response = await _simulationService.RunAsync("insertion-sort", input, null);
        return Ok(response);
    }

    /// <summary>
    /// POST /api/simulation/run
    /// Runs a simulation for the requested algorithm and input array.
    /// </summary>
    /// <response code="200">Simulation completed.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="501">Requested algorithm is not supported.</response>
    [HttpPost("run")]
    [ProducesResponseType(typeof(backend.Models.Simulations.SimulationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Run([FromBody] RunSimulationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Algorithm))
            ModelState.AddModelError(nameof(dto.Algorithm), "Algorithm is required.");

        if (dto.Array is null || dto.Array.Length == 0)
            ModelState.AddModelError(nameof(dto.Array), "Array must contain at least one value.");

        if (!string.IsNullOrWhiteSpace(dto.Algorithm))
        {
            var normalizedAlgorithm = dto.Algorithm.Trim().ToLowerInvariant();
            if (normalizedAlgorithm is "binary_search" or "binary-search" && dto.Target is null)
                ModelState.AddModelError(nameof(dto.Target), "Target is required for binary search.");
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // NotSupportedException → 501 via GlobalExceptionMiddleware
        var response = await _simulationService.RunAsync(dto.Algorithm, dto.Array!, dto.Target);
        return Ok(response);
    }

    /// <summary>
    /// POST /api/simulation/start
    /// Starts a stateful practice-mode session for the requested algorithm and input array.
    /// </summary>
    /// <response code="200">Session started.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="501">Requested algorithm is not supported.</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(backend.Models.Simulations.SimulationSession), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Start([FromBody] StartSimulationSessionRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Algorithm))
            ModelState.AddModelError(nameof(dto.Algorithm), "Algorithm is required.");

        if (dto.Array is null || dto.Array.Length == 0)
            ModelState.AddModelError(nameof(dto.Array), "Array must contain at least one value.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // NotSupportedException → 501 via GlobalExceptionMiddleware
        var session = await _simulationService.StartSessionAsync(dto.Algorithm, dto.Array!, dto.Target);
        return Ok(session);
    }

    /// <summary>
    /// POST /api/simulation/validate-step
    /// Validates a learner's action against the next correct algorithm move.
    /// </summary>
    /// <response code="200">Step validated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="404">Session not found.</response>
    /// <response code="501">Requested algorithm is not supported.</response>
    [HttpPost("validate-step")]
    [ProducesResponseType(typeof(backend.Models.Simulations.SimulationValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateStep([FromBody] ValidateSimulationStepRequestDto dto)
    {
        var userAction = dto.ResolvedAction;

        if (string.IsNullOrWhiteSpace(dto.SessionId))
            ModelState.AddModelError(nameof(dto.SessionId), "SessionId is required.");

        if (userAction is null)
        {
            ModelState.AddModelError(nameof(dto.Action), "Action is required.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(userAction.Type))
                ModelState.AddModelError($"{nameof(dto.Action)}.{nameof(SimulationUserActionDto.Type)}", "Action type is required.");

            var normalizedType = userAction.Type.Trim().ToLowerInvariant();
            var isDecisionType = normalizedType is "left" or "right" or "found"
                or "go_left" or "go_right"
                or "discard_left" or "discard_right"
                or "target_found";
            var requiredIndices = isDecisionType
                ? 0
                : (normalizedType is "midpoint" or "midpoint_pick" or "pick_midpoint" or "place" or "select_min" ? 1 : 2);

            if (requiredIndices > 0 && (userAction.Indices is null || userAction.Indices.Length < requiredIndices))
            {
                ModelState.AddModelError(
                    $"{nameof(dto.Action)}.{nameof(SimulationUserActionDto.Indices)}",
                    requiredIndices == 1
                        ? "At least one index is required for midpoint selection."
                        : "At least two indices are required.");
            }
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // KeyNotFoundException   → 404 via GlobalExceptionMiddleware
        // NotSupportedException  → 501 via GlobalExceptionMiddleware
        var validatedAction = userAction!;
        var response = await _simulationService.ValidateStepAsync(
            dto.SessionId,
            validatedAction.Type,
            validatedAction.Indices ?? Array.Empty<int>());

        return Ok(response);
    }
}
