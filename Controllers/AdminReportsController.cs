using System.Globalization;
using System.Text;
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
    private readonly ILogger<AdminReportsController> _logger;

    public AdminReportsController(
        IReportService reportService,
        IReportCsvExportService reportCsvExportService,
        ILogger<AdminReportsController> logger)
    {
        _reportService = reportService;
        _reportCsvExportService = reportCsvExportService;
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

        if (!DateTime.TryParse(startDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedStartDate) ||
            !DateTime.TryParse(endDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedEndDate))
        {
            return BadRequest(new { message = "Invalid date format" });
        }

        if (parsedStartDate > parsedEndDate)
        {
            return BadRequest(new { message = "startDate must not be greater than endDate." });
        }

        var bundle = await _reportService.GetAdminReportBundleAsync(parsedStartDate, parsedEndDate);
        var normalizedFormat = format.Trim().ToLowerInvariant();

        if (normalizedFormat == "csv")
        {
            var csvStream = _reportCsvExportService.CreateAdminReportCsv(bundle, parsedStartDate, parsedEndDate);
            return File(csvStream, "text/csv", $"admin-report-{parsedStartDate:yyyyMMdd}-{parsedEndDate:yyyyMMdd}.csv");
        }

        if (normalizedFormat == "pdf")
        {
            var pdf = BuildPdf(bundle, parsedStartDate, parsedEndDate);
            return File(pdf, "application/pdf", $"admin-report-{parsedStartDate:yyyyMMdd}-{parsedEndDate:yyyyMMdd}.pdf");
        }

        return BadRequest(new { message = "Invalid format" });
    }

    private static byte[] BuildPdf(AdminReportBundleDto bundle, DateTime startDate, DateTime endDate)
    {
        var lines = new List<string>
        {
            $"Admin Report ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd})",
            $"Summary: Attempts={bundle.Summary.TotalAttempts}, Students={bundle.Summary.TotalStudents}, AvgScore={bundle.Summary.AverageScore:F2}, XP={bundle.Summary.TotalXp}",
            string.Empty,
            "Per Student Breakdown"
        };

        lines.AddRange(bundle.PerStudent.Select(item =>
            $"{item.StudentName} | Attempts={item.TotalAttempts} | Avg={item.AverageScore:F2} | Best={item.BestScore} | XP={item.TotalXp} | Algorithms={item.AlgorithmsAttempted}"));
        lines.Add(string.Empty);
        lines.Add("Per Algorithm Breakdown");
        lines.AddRange(bundle.PerAlgorithm.Select(item =>
            $"{item.AlgorithmType} | Attempts={item.AttemptCount} | Avg={item.AverageScore:F2} | PassRate={item.PassRate:F2}"));
        lines.Add(string.Empty);
        lines.Add("Per Quiz Breakdown");
        lines.AddRange(bundle.PerQuiz.Select(item =>
            $"{item.Title} | Attempts={item.AttemptCount} | Avg={item.AverageScore:F2} | High={item.HighestScore} | Low={item.LowestScore}"));

        return CreateSimplePdf(lines);
    }

    private static byte[] CreateSimplePdf(IReadOnlyList<string> lines)
    {
        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("BT");
        contentBuilder.AppendLine("/F1 10 Tf");
        contentBuilder.AppendLine("14 TL");
        contentBuilder.AppendLine("50 780 Td");

        for (var i = 0; i < lines.Count; i++)
        {
            var escaped = EscapePdf(lines[i]);
            if (i == 0)
            {
                contentBuilder.AppendLine($"({escaped}) Tj");
            }
            else
            {
                contentBuilder.AppendLine("T*");
                contentBuilder.AppendLine($"({escaped}) Tj");
            }
        }

        contentBuilder.AppendLine("ET");
        var contentBytes = Encoding.ASCII.GetBytes(contentBuilder.ToString());

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");
        var offsets = new List<long> { 0 };

        void WriteObject(string value)
        {
            offsets.Add(stream.Position);
            writer.Write(value);
        }

        writer.Flush();
        WriteObject("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
        writer.Flush();
        WriteObject("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
        writer.Flush();
        WriteObject("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");
        writer.Flush();
        WriteObject("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
        writer.Flush();
        offsets.Add(stream.Position);
        writer.Write($"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        writer.Flush();
        stream.Write(contentBytes, 0, contentBytes.Length);
        writer.Write("endstream\nendobj\n");
        writer.Flush();

        var xrefPosition = stream.Position;
        writer.Write("xref\n");
        writer.Write($"0 {offsets.Count}\n");
        writer.Write("0000000000 65535 f \n");

        for (var i = 1; i < offsets.Count; i++)
        {
            writer.Write($"{offsets[i]:0000000000} 00000 n \n");
        }

        writer.Write("trailer\n");
        writer.Write($"<< /Size {offsets.Count} /Root 1 0 R >>\n");
        writer.Write("startxref\n");
        writer.Write($"{xrefPosition}\n");
        writer.Write("%%EOF");
        writer.Flush();

        return stream.ToArray();
    }

    private static string EscapePdf(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        return escaped;
    }
}
