namespace backend.Models;

/// <summary>
/// Domain model representing a row in the quizzes table.
/// </summary>
public class Quiz
{
    public int QuizId { get; set; }
    public int AlgorithmId { get; set; }
    public int CreatedBy { get; set; }        // FK → Users(Id)
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? TimeLimitMins { get; set; }   // null = no time limit
    public int PassScore { get; set; } = 70;  // percentage 0–100
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
