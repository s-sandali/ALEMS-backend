using backend.DTOs;

namespace backend.Repositories;

/// <summary>
/// Data-access contract for report data aggregations.
/// Provides parameterized queries for generating reports with flexible date ranges.
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Retrieves per-student breakdown of quiz attempts and performance metrics.
    /// Groups data by student and includes totals, averages, and XP earned within the specified date range.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>
    /// IEnumerable of <see cref="PerStudentReportDto"/> containing student performance data.
    /// Returns empty collection if no attempts exist in the date range.
    /// </returns>
    Task<IEnumerable<PerStudentReportDto>> GetPerStudentReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Retrieves per-algorithm breakdown of quiz attempt statistics.
    /// Groups data by algorithm type and includes attempt counts, average scores, and pass rates.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>
    /// IEnumerable of <see cref="PerAlgorithmReportDto"/> containing algorithm performance data.
    /// Returns empty collection if no attempts exist in the date range.
    /// </returns>
    Task<IEnumerable<PerAlgorithmReportDto>> GetPerAlgorithmReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Retrieves per-quiz breakdown of attempt statistics and performance metrics.
    /// Groups data by quiz and includes attempt counts, average scores, highest, and lowest scores.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>
    /// IEnumerable of <see cref="PerQuizReportDto"/> containing quiz performance data.
    /// Returns empty collection if no attempts exist in the date range.
    /// </returns>
    Task<IEnumerable<PerQuizReportDto>> GetPerQuizReportAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Retrieves overall summary statistics for a given date range.
    /// Returns aggregate counts and averages across all attempts.
    /// </summary>
    /// <param name="startDate">Inclusive start date for the date range filter.</param>
    /// <param name="endDate">Inclusive end date for the date range filter.</param>
    /// <returns>
    /// <see cref="SummaryStatisticsDto"/> containing total attempts, students, average score, and total XP.
    /// Returns a result with zero values if no attempts exist in the date range.
    /// </returns>
    Task<SummaryStatisticsDto> GetSummaryStatisticsAsync(DateTime startDate, DateTime endDate);
}
