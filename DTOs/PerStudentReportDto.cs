namespace backend.DTOs;

/// <summary>
/// DTO representing a per-student breakdown of quiz attempts and performance metrics.
/// </summary>
public class PerStudentReportDto
{
    /// <summary>
    /// The unique identifier of the student.
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// The name of the student.
    /// </summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of quiz attempts made by this student within the date range.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Average score across all quiz attempts (percentage).
    /// </summary>
    public decimal AverageScore { get; set; }

    /// <summary>
    /// Best score achieved by the student (percentage).
    /// </summary>
    public int BestScore { get; set; }

    /// <summary>
    /// Total XP earned from all quiz attempts within the date range.
    /// </summary>
    public int TotalXp { get; set; }

    /// <summary>
    /// Number of distinct quizzes/algorithms attempted by the student.
    /// </summary>
    public int AlgorithmsAttempted { get; set; }
}
