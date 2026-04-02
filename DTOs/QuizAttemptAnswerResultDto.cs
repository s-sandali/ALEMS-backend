namespace backend.DTOs;

/// <summary>
/// Per-answer grading details returned after a quiz attempt is submitted.
/// </summary>
public class QuizAttemptAnswerResultDto
{
    public int QuestionId { get; set; }
    public string SelectedOption { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    /// <summary>
    /// Explanation revealed only after submission — null when no explanation was authored.
    /// </summary>
    public string? Explanation { get; set; }
}
