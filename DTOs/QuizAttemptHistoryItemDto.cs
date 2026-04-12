namespace backend.DTOs;

/// <summary>
/// Represents a single quiz attempt row in the student dashboard history table.
/// Joins quiz_attempts with quizzes and algorithms to provide display-ready data.
/// </summary>
public class QuizAttemptHistoryItemDto
{
    /// <summary>
    /// Primary key of the quiz attempt.
    /// </summary>
    public int AttemptId { get; set; }

    /// <summary>
    /// The quiz that was attempted.
    /// </summary>
    public int QuizId { get; set; }

    /// <summary>
    /// Display title of the quiz (from quizzes.title).
    /// </summary>
    public string QuizTitle { get; set; } = string.Empty;

    /// <summary>
    /// Name of the algorithm the quiz belongs to (from algorithms.Name).
    /// </summary>
    public string AlgorithmName { get; set; } = string.Empty;

    /// <summary>
    /// Number of correct answers in this attempt.
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Total number of questions in the quiz.
    /// </summary>
    public int TotalQuestions { get; set; }

    /// <summary>
    /// Percentage score calculated as (Score / TotalQuestions) * 100.
    /// </summary>
    public double ScorePercent { get; set; }

    /// <summary>
    /// XP awarded for this attempt. Zero on retries.
    /// </summary>
    public int XpEarned { get; set; }

    /// <summary>
    /// Whether the student met the pass threshold for this quiz.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Timestamp when the attempt was completed. Null if the attempt was never finished.
    /// </summary>
    public DateTime? CompletedAt { get; set; }
}
