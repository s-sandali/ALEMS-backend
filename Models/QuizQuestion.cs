namespace backend.Models;

/// <summary>
/// Domain model representing a row in the quiz_questions table.
/// </summary>
public class QuizQuestion
{
    public int QuestionId { get; set; }
    public int QuizId { get; set; }
    public string QuestionType { get; set; } = "MCQ";   // 'MCQ' | 'PREDICT_STEP'
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = "A";    // 'A' | 'B' | 'C' | 'D'
    public string Difficulty { get; set; } = "easy";    // 'easy' | 'medium' | 'hard'
    public int XpReward { get; set; }
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
