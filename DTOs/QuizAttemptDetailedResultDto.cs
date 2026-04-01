namespace backend.DTOs;

/// <summary>
/// Per-question grading outcome returned after a quiz attempt is submitted.
/// </summary>
public class QuizAttemptDetailedResultDto
{
    public int QuestionId { get; set; }
    public bool IsCorrect { get; set; }
}
