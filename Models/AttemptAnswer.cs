namespace backend.Models;

/// <summary>
/// Domain model representing a row in the attempt_answers table.
/// </summary>
public class AttemptAnswer
{
    public int AnswerId { get; set; }
    public int AttemptId { get; set; }
    public int QuestionId { get; set; }
    public string SelectedOption { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
