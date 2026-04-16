using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Service contract for generating report data with flexible date range queries.
/// Provides access to aggregate statistics and breakdowns by student, algorithm, and quiz.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates the full admin report bundle for export.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>Aggregated report bundle containing summary and breakdown datasets.</returns>
    Task<AdminReportBundleDto> GetAdminReportBundleAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generates a per-student breakdown of quiz attempts and performance metrics for a date range.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>IEnumerable of PerStudentReportDto with student performance data.</returns>
    Task<IEnumerable<PerStudentReportDto>> GetPerStudentReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generates a per-algorithm breakdown of quiz attempt statistics for a date range.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>IEnumerable of PerAlgorithmReportDto with algorithm performance data.</returns>
    Task<IEnumerable<PerAlgorithmReportDto>> GetPerAlgorithmReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generates a per-quiz breakdown of attempt statistics and performance metrics for a date range.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>IEnumerable of PerQuizReportDto with quiz performance data.</returns>
    Task<IEnumerable<PerQuizReportDto>> GetPerQuizReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Generates overall summary statistics for a given date range.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>SummaryStatisticsDto with aggregate statistics.</returns>
    Task<SummaryStatisticsDto> GetSummaryStatisticsAsync(DateTime startDate, DateTime endDate);
}
