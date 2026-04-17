namespace backend.DTOs;

/// <summary>
/// Represents statistics for a specific quiz.
/// </summary>
public class QuizStatsDto
{
    /// <summary>
    /// Total number of attempts submitted for this quiz across all users.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Average score (percentage 0–100) across all attempts.
    /// Value is 0 if no attempts exist.
    /// </summary>
    public double AverageScore { get; set; }

    /// <summary>
    /// Pass rate (percentage 0–100) — percentage of attempts marked as passed.
    /// Value is 0 if no attempts exist.
    /// </summary>
    public double PassRate { get; set; }
}
