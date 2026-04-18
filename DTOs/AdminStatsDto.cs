namespace backend.DTOs;

/// <summary>
/// Platform-wide statistics response DTO.
/// </summary>
public class AdminStatsDto
{
    /// <summary>
    /// Total number of registered users on the platform.
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Total number of quizzes created on the platform.
    /// </summary>
    public int TotalQuizzes { get; set; }

    /// <summary>
    /// Total number of quiz attempts across all users.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Platform-wide average pass rate as a percentage (0-100).
    /// Calculated as: (number of passed attempts / total attempts) * 100
    /// </summary>
    public double AveragePassRate { get; set; }
}
