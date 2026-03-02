using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace backend.Controllers;

/// <summary>
/// Provides infrastructure health-check endpoints.
/// </summary>
[ApiController]
[Route("api/test")]
[AllowAnonymous]           // public — no JWT required
[Produces("application/json")]
public class TestController : ControllerBase
{
    private readonly DatabaseHelper _databaseHelper;
    private readonly ILogger<TestController> _logger;

    public TestController(DatabaseHelper databaseHelper, ILogger<TestController> logger)
    {
        _databaseHelper = databaseHelper;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/test/test-db
    /// Opens a MySQL connection and returns 200 OK on success,
    /// or 500 with a structured error payload on failure.
    /// </summary>
    /// <response code="200">Database connection is healthy.</response>
    /// <response code="500">MySQL connection failed or unexpected error.</response>
    [HttpGet("test-db")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TestDatabase()
    {
        try
        {
            await using var connection = await _databaseHelper.OpenConnectionAsync();

            _logger.LogInformation("Database connection test succeeded. Server: {Server}, Database: {Database}",
                connection.DataSource, connection.Database);

            return Ok(new
            {
                status = "success",
                message = "Database connection is healthy.",
                server = connection.DataSource,
                database = connection.Database
            });
        }
        catch (MySqlException ex)
        {
            _logger.LogError(ex, "MySQL error during database connection test. Error code: {Code}", ex.Number);

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "Database connection failed.",
                errorCode = ex.Number,
                detail = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during database connection test.");

            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while testing the database connection.",
                detail = ex.Message
            });
        }
    }
}
