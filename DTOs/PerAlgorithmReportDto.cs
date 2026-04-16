namespace backend.DTOs;

/// <summary>
/// DTO representing a per-algorithm breakdown of quiz attempt statistics.
/// </summary>
public class PerAlgorithmReportDto
{
    /// <summary>
    /// The algorithm type or category (e.g., "Sorting", "Graph", "Dynamic Programming").
    /// </summary>
    public string AlgorithmType { get; set; } = string.Empty;

    /// <summary>
    /// Total number of attempts across all students for this algorithm.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Average score across all attempts for this algorithm (percentage).
    /// </summary>
    public decimal AverageScore { get; set; }

    /// <summary>
    /// Pass rate percentage (% of attempts scoring >= 50).
    /// </summary>
    public decimal PassRate { get; set; }
}
