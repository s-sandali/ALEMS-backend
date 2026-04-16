using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Provides endpoints for generating report data with parameterized date-range filters.
/// Supports per-student, per-algorithm, and per-quiz breakdowns, as well as summary statistics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(IReportService reportService, ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a per-student breakdown of quiz attempts and performance metrics for a date range.
    /// Groups data by student and includes totals, average scores, best scores, XP earned, and distinct algorithms attempted.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <param name="endDate">Inclusive end date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <returns>
    /// 200 OK: List of PerStudentReportDto containing student performance data.
    /// 400 Bad Request: Invalid date format or startDate > endDate.
    /// 401 Unauthorized: Missing or invalid Clerk JWT token.
    /// 500 Internal Server Error: Database or service error.
    /// </returns>
    [HttpGet("per-student")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PerStudentReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPerStudentReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
        {
            _logger.LogWarning("Invalid date range: startDate ({StartDate}) > endDate ({EndDate})", startDate, endDate);
            return BadRequest(new { message = "startDate must not be greater than endDate." });
        }

        try
        {
            var report = await _reportService.GetPerStudentReportAsync(startDate, endDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving per-student report for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while retrieving the per-student report."
            });
        }
    }

    /// <summary>
    /// Retrieves a per-algorithm breakdown of quiz attempt statistics for a date range.
    /// Groups data by algorithm type and includes attempt counts, average scores, and pass rates.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <param name="endDate">Inclusive end date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <returns>
    /// 200 OK: List of PerAlgorithmReportDto containing algorithm performance data.
    /// 400 Bad Request: Invalid date format or startDate > endDate.
    /// 401 Unauthorized: Missing or invalid Clerk JWT token.
    /// 500 Internal Server Error: Database or service error.
    /// </returns>
    [HttpGet("per-algorithm")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PerAlgorithmReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPerAlgorithmReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
        {
            _logger.LogWarning("Invalid date range: startDate ({StartDate}) > endDate ({EndDate})", startDate, endDate);
            return BadRequest(new { message = "startDate must not be greater than endDate." });
        }

        try
        {
            var report = await _reportService.GetPerAlgorithmReportAsync(startDate, endDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving per-algorithm report for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while retrieving the per-algorithm report."
            });
        }
    }

    /// <summary>
    /// Retrieves a per-quiz breakdown of attempt statistics and performance metrics for a date range.
    /// Groups data by quiz and includes attempt counts, average scores, highest, and lowest scores.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <param name="endDate">Inclusive end date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <returns>
    /// 200 OK: List of PerQuizReportDto containing quiz performance data.
    /// 400 Bad Request: Invalid date format or startDate > endDate.
    /// 401 Unauthorized: Missing or invalid Clerk JWT token.
    /// 500 Internal Server Error: Database or service error.
    /// </returns>
    [HttpGet("per-quiz")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<PerQuizReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPerQuizReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
        {
            _logger.LogWarning("Invalid date range: startDate ({StartDate}) > endDate ({EndDate})", startDate, endDate);
            return BadRequest(new { message = "startDate must not be greater than endDate." });
        }

        try
        {
            var report = await _reportService.GetPerQuizReportAsync(startDate, endDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving per-quiz report for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while retrieving the per-quiz report."
            });
        }
    }

    /// <summary>
    /// Retrieves overall summary statistics for a given date range.
    /// Returns aggregate counts, student counts, and averages across all attempts.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <param name="endDate">Inclusive end date for the date range filter (format: yyyy-MM-dd or ISO 8601).</param>
    /// <returns>
    /// 200 OK: SummaryStatisticsDto containing total attempts, students, average score, and total XP.
    /// 400 Bad Request: Invalid date format or startDate > endDate.
    /// 401 Unauthorized: Missing or invalid Clerk JWT token.
    /// 500 Internal Server Error: Database or service error.
    /// </returns>
    [HttpGet("summary")]
    [Authorize]
    [ProducesResponseType(typeof(SummaryStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSummaryStatistics(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate > endDate)
        {
            _logger.LogWarning("Invalid date range: startDate ({StartDate}) > endDate ({EndDate})", startDate, endDate);
            return BadRequest(new { message = "startDate must not be greater than endDate." });
        }

        try
        {
            var stats = await _reportService.GetSummaryStatisticsAsync(startDate, endDate);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving summary statistics for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "An error occurred while retrieving summary statistics."
            });
        }
    }
}
