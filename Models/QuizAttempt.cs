namespace backend.Models;

/// <summary>
/// Domain model representing a row in the quiz_attempts table.
/// </summary>
public class QuizAttempt
{
    public int AttemptId { get; set; }
    public int UserId { get; set; }
    public int QuizId { get; set; }
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public int XpEarned { get; set; }

    // Retained because the existing schema already includes this column.
    public bool Passed { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
