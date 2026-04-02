namespace backend.DTOs;

/// <summary>
/// Response DTO returned after a quiz attempt is submitted.
/// </summary>
public class QuizAttemptResultDto
{
    public int AttemptId { get; set; }
    public int QuizId { get; set; }
    /// <summary>Percentage score 0–100 (rounded).</summary>
    public int Score { get; set; }
    /// <summary>Raw number of correct answers.</summary>
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public int XpEarned { get; set; }
    public bool Passed { get; set; }
    /// <summary>
    /// True when this is the user's first attempt at this quiz and XP was actually awarded.
    /// False on retries — XP is not re-awarded so the UI can show an appropriate message.
    /// </summary>
    public bool IsFirstAttempt { get; set; }
    public List<QuizAttemptAnswerResultDto> Results { get; set; } = [];
}
