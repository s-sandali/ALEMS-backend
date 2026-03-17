using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Endpoints for running algorithm simulations.
/// </summary>
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
    /// POST /api/simulation/run
    /// Runs a simulation for the requested algorithm and input array.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(backend.Models.Simulations.SimulationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Run([FromBody] RunSimulationRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Algorithm))
            {
                ModelState.AddModelError(nameof(dto.Algorithm), "Algorithm is required.");
            }

            if (dto.Array is null || dto.Array.Length == 0)
            {
                ModelState.AddModelError(nameof(dto.Array), "Array must contain at least one value.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var response = await _simulationService.RunAsync(dto.Algorithm, dto.Array!);
            return Ok(response);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Unsupported simulation algorithm requested: {Algorithm}", dto.Algorithm);
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                status = "error",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in POST /api/simulation/run");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while running the simulation.",
                detail = ex.Message
            });
        }
    }

    /// <summary>
    /// POST /api/simulation/validate-step
    /// Validates a learner's action against the next correct algorithm move.
    /// </summary>
    [HttpPost("validate-step")]
    [ProducesResponseType(typeof(backend.Models.Simulations.SimulationValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status501NotImplemented)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateStep([FromBody] ValidateSimulationStepRequestDto dto)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dto.Algorithm))
            {
                ModelState.AddModelError(nameof(dto.Algorithm), "Algorithm is required.");
            }

            if (dto.CurrentArray is null || dto.CurrentArray.Length == 0)
            {
                ModelState.AddModelError(nameof(dto.CurrentArray), "CurrentArray must contain at least one value.");
            }

            if (dto.UserAction is null)
            {
                ModelState.AddModelError(nameof(dto.UserAction), "UserAction is required.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dto.UserAction.Type))
                {
                    ModelState.AddModelError($"{nameof(dto.UserAction)}.{nameof(dto.UserAction.Type)}", "Action type is required.");
                }

                if (dto.UserAction.Indices is null || dto.UserAction.Indices.Length < 2)
                {
                    ModelState.AddModelError($"{nameof(dto.UserAction)}.{nameof(dto.UserAction.Indices)}", "At least two indices are required.");
                }
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userAction = dto.UserAction!;
            var response = await _simulationService.ValidateStepAsync(
                dto.Algorithm,
                dto.CurrentArray!,
                userAction.Type,
                userAction.Indices!);

            return Ok(response);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Unsupported simulation algorithm requested for validation: {Algorithm}", dto.Algorithm);
            return StatusCode(StatusCodes.Status501NotImplemented, new
            {
                status = "error",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in POST /api/simulation/validate-step");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while validating the simulation step.",
                detail = ex.Message
            });
        }
    }
}
