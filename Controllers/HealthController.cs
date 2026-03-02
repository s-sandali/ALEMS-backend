using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Health check endpoint for monitoring systems and Azure App Service.
/// Returns application and database connectivity status.
/// Always returns HTTP 200 so monitoring tools can parse the body for degraded state.
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]           // public — no JWT required
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly DatabaseHelper _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(DatabaseHelper db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/health
    /// Checks application liveness and database connectivity.
    /// </summary>
    /// <remarks>
    /// Always returns <b>200 OK</b>. Parse the <c>status</c> field in the body
    /// (<c>Healthy</c> | <c>Degraded</c>) to determine the actual state.
    /// </remarks>
    /// <response code="200">Application is running; body contains db connectivity status.</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealth()
    {
        var dbStatus = "Connected";
        var overallStatus = "Healthy";

        try
        {
            // Attempt a lightweight database connection + query
            await using var connection = await _db.OpenConnectionAsync();
            await using var cmd = new MySql.Data.MySqlClient.MySqlCommand("SELECT 1", connection);
            await cmd.ExecuteScalarAsync();

            _logger.LogInformation("Health check: Database connected successfully");
        }
        catch (Exception ex)
        {
            dbStatus = "Disconnected";
            overallStatus = "Degraded";

            _logger.LogWarning(ex, "Health check: Database connection failed — {Message}", ex.Message);
        }

        var response = new
        {
            status = overallStatus,
            database = dbStatus,
            timestamp = DateTime.UtcNow
        };

        // Always return 200 so monitoring tools (Azure, uptime checkers) see a valid response
        // and parse the body to determine if the service is degraded
        return Ok(response);
    }
}
