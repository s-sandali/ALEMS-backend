namespace backend.DTOs;

/// <summary>
/// Represents a student's quiz coverage for a single algorithm.
/// Aggregated from quiz_attempts joined through quizzes to algorithms.
/// Used in the Algorithm Coverage section of the student dashboard.
/// </summary>
public class AlgorithmCoverageItemDto
{
    /// <summary>
    /// Primary key of the algorithm.
    /// </summary>
    public int AlgorithmId { get; set; }

    /// <summary>
    /// Display name of the algorithm (from algorithms.Name).
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// Category the algorithm belongs to (e.g. "Sorting", "Searching").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Total number of quiz attempts the student has made for this algorithm.
    /// Zero if the student has never attempted a quiz for this algorithm.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Number of quiz attempts where the student met the pass threshold.
    /// </summary>
    public int PassedAttempts { get; set; }

    /// <summary>
    /// The highest score percentage achieved across all attempts for this algorithm.
    /// Zero if the student has never attempted a quiz for this algorithm.
    /// </summary>
    public double BestScorePercent { get; set; }

    /// <summary>
    /// True if the student has at least one passing attempt for this algorithm's quiz.
    /// Drives the "complete" status badge in the frontend.
    /// </summary>
    public bool HasPassedQuiz { get; set; }
}
