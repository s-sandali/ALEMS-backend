using System.Globalization;
using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Admin-only report export endpoint.
/// Returns a combined report bundle as CSV or PDF.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("text/csv", "application/pdf", "application/json")]
public class AdminReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IReportCsvExportService _reportCsvExportService;
    private readonly IReportPdfExportService _reportPdfExportService;
    private readonly ILogger<AdminReportsController> _logger;

    public AdminReportsController(
        IReportService reportService,
        IReportCsvExportService reportCsvExportService,
        IReportPdfExportService reportPdfExportService,
        ILogger<AdminReportsController> logger)
    {
        _reportService = reportService;
        _reportCsvExportService = reportCsvExportService;
        _reportPdfExportService = reportPdfExportService;
        _logger = logger;
    }

    /// <summary>
    /// Exports report data in CSV or PDF format for an admin user.
    /// </summary>
    /// <param name="format">Output format: csv or pdf.</param>
    /// <param name="startDate">Inclusive start date.</param>
    /// <param name="endDate">Inclusive end date.</param>
    [HttpGet("reports")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? format,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate)
    {
        if (string.IsNullOrWhiteSpace(format) || string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
        {
            return BadRequest(new { message = "Missing parameters" });
        }

        if (!TryParseDate(startDate, out var parsedStartDate) ||
            !TryParseDate(endDate, out var parsedEndDate))
        {
            return BadRequest(new { error = "Invalid date range" });
        }

        if (parsedStartDate > parsedEndDate)
        {
            return BadRequest(new { error = "Invalid date range" });
        }

        var bundle = await _reportService.GetAdminReportBundleAsync(parsedStartDate, parsedEndDate);
        var isEmptyResult = bundle.Summary.TotalAttempts == 0
            && !bundle.PerStudent.Any()
            && !bundle.PerAlgorithm.Any()
            && !bundle.PerQuiz.Any();

        var normalizedFormat = format.Trim().ToLowerInvariant();

        if (normalizedFormat == "csv")
        {
            if (isEmptyResult)
            {
                return File(Array.Empty<byte>(), "text/csv", $"admin-report-{parsedStartDate:yyyyMMdd}-{parsedEndDate:yyyyMMdd}.csv");
            }

            var csvStream = _reportCsvExportService.CreateAdminReportCsv(bundle, parsedStartDate, parsedEndDate);
            return File(csvStream, "text/csv", $"admin-report-{parsedStartDate:yyyyMMdd}-{parsedEndDate:yyyyMMdd}.csv");
        }

        if (normalizedFormat == "pdf")
        {
            if (isEmptyResult)
            {
                return File(Array.Empty<byte>(), "application/pdf", $"admin-report-{parsedStartDate:yyyyMMdd}-{parsedEndDate:yyyyMMdd}.pdf");
            }

            var pdf = _reportPdfExportService.CreateAdminReportPdf(bundle, parsedStartDate, parsedEndDate);
            return File(pdf, "application/pdf", $"admin-report-{parsedStartDate:yyyyMMdd}-{parsedEndDate:yyyyMMdd}.pdf");
        }

        return BadRequest(new { message = "Invalid format" });
    }

    private static bool TryParseDate(string value, out DateTime parsedDate)
    {
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "O"
        };

        return DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsedDate);
    }

}
