namespace backend.DTOs;

/// <summary>
/// DTO representing overall summary statistics for a given date range.
/// </summary>
public class SummaryStatisticsDto
{
    /// <summary>
    /// Total number of quiz attempts within the date range.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Total number of unique students who made at least one attempt.
    /// </summary>
    public int TotalStudents { get; set; }

    /// <summary>
    /// Average score across all attempts (percentage).
    /// </summary>
    public decimal AverageScore { get; set; }

    /// <summary>
    /// Total XP earned across all attempts within the date range.
    /// </summary>
    public int TotalXp { get; set; }
}
