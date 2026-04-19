namespace backend.DTOs;

/// <summary>
/// DTO representing a per-quiz breakdown of attempt statistics and performance metrics.
/// </summary>
public class PerQuizReportDto
{
    /// <summary>
    /// The title of the quiz.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Total number of attempts for this quiz within the date range.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Average score across all attempts for this quiz (percentage).
    /// </summary>
    public decimal AverageScore { get; set; }

    /// <summary>
    /// The highest score achieved on this quiz (percentage).
    /// </summary>
    public int HighestScore { get; set; }

    /// <summary>
    /// The lowest score achieved on this quiz (percentage).
    /// </summary>
    public int LowestScore { get; set; }
}
