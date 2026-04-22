using backend.DTOs;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Service for generating report data with flexible date range queries.
/// Coordinates report repository calls and provides aggregate statistics and breakdowns.
/// </summary>
public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;
    private readonly ILogger<ReportService> _logger;

    public ReportService(IReportRepository reportRepository, ILogger<ReportService> logger)
    {
        _reportRepository = reportRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminReportBundleDto> GetAdminReportBundleAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation(
            "Fetching admin report bundle for date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            // Run sequentially to avoid opening 4 concurrent DB connections per request.
            // Under load tests, parallel fan-out here can exhaust MySQL connection limits.
            var summary = await _reportRepository.GetSummaryStatisticsAsync(startDate, endDate);
            var perStudent = await _reportRepository.GetPerStudentReportAsync(startDate, endDate);
            var perAlgorithm = await _reportRepository.GetPerAlgorithmReportAsync(startDate, endDate);
            var perQuiz = await _reportRepository.GetPerQuizReportAsync(startDate, endDate);

            return new AdminReportBundleDto
            {
                Summary = summary,
                PerStudent = perStudent,
                PerAlgorithm = perAlgorithm,
                PerQuiz = perQuiz
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching admin report bundle for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PerStudentReportDto>> GetPerStudentReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation(
            "Fetching per-student report for date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            var report = await _reportRepository.GetPerStudentReportAsync(startDate, endDate);
            _logger.LogInformation("Per-student report retrieved with {Count} records", report.Count());
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching per-student report for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PerAlgorithmReportDto>> GetPerAlgorithmReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation(
            "Fetching per-algorithm report for date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            var report = await _reportRepository.GetPerAlgorithmReportAsync(startDate, endDate);
            _logger.LogInformation("Per-algorithm report retrieved with {Count} records", report.Count());
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching per-algorithm report for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<PerQuizReportDto>> GetPerQuizReportAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation(
            "Fetching per-quiz report for date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            var report = await _reportRepository.GetPerQuizReportAsync(startDate, endDate);
            _logger.LogInformation("Per-quiz report retrieved with {Count} records", report.Count());
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching per-quiz report for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<SummaryStatisticsDto> GetSummaryStatisticsAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation(
            "Fetching summary statistics for date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
            startDate, endDate);

        try
        {
            var stats = await _reportRepository.GetSummaryStatisticsAsync(startDate, endDate);
            _logger.LogInformation(
                "Summary statistics retrieved: {TotalAttempts} attempts, {TotalStudents} students, avg score: {AvgScore:F2}",
                stats.TotalAttempts, stats.TotalStudents, stats.AverageScore);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching summary statistics for date range {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                startDate, endDate);
            throw;
        }
    }
}
