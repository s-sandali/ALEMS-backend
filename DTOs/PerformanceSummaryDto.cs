namespace backend.DTOs;

/// <summary>
/// Aggregate performance statistics for a student across all quiz attempts.
/// Used in the Performance Summary section of the student dashboard.
/// </summary>
public class PerformanceSummaryDto
{
    /// <summary>
    /// Total number of quiz attempts the student has submitted.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Total number of quiz attempts where the student met the pass threshold.
    /// </summary>
    public int TotalPassed { get; set; }

    /// <summary>
    /// Percentage of attempts that resulted in a pass.
    /// Calculated as (TotalPassed / TotalAttempts) * 100. Zero if TotalAttempts is zero.
    /// </summary>
    public double PassRate { get; set; }

    /// <summary>
    /// Average percentage score across all quiz attempts.
    /// Calculated as AVG(score / total_questions * 100). Zero if TotalAttempts is zero.
    /// </summary>
    public double AverageScore { get; set; }

    /// <summary>
    /// Sum of all XP earned from quiz attempts.
    /// Retries contribute 0 XP, so this reflects first-attempt XP only.
    /// </summary>
    public int TotalXpFromQuizzes { get; set; }
}
