using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only endpoints for platform statistics and aggregations.
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT with the <b>Admin</b> role.
/// </remarks>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService adminService, ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/admin/stats — Retrieve platform-wide statistics.
    /// </summary>
    /// <remarks>
    /// Returns aggregate statistics including total users, total quizzes,
    /// total quiz attempts, and the platform-wide average pass rate.
    /// </remarks>
    /// <response code="200">Statistics retrieved successfully.</response>
    /// <response code="401">Unauthorized; valid Clerk JWT required.</response>
    /// <response code="403">Forbidden; Admin role required.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AdminStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPlatformStats()
    {
        try
        {
            _logger.LogInformation("Admin requesting platform statistics");
            var stats = await _adminService.GetPlatformStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving platform statistics");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving platform statistics."
            });
        }
    }
}
