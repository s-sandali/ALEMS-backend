namespace backend.DTOs;

/// <summary>
/// Read model returned by quiz question endpoints.
/// The <c>correct_option</c> field is intentionally included here for admin use.
/// Student-facing endpoints should use a separate DTO that omits it.
/// </summary>
public class QuizQuestionResponseDto
{
    public int QuestionId { get; set; }
    public int QuizId { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
