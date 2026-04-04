using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Payload for updating an existing quiz question.
/// All fields are required — send the full current state plus any changes.
/// </summary>
public class UpdateQuizQuestionDto
{
    [Required]
    [RegularExpression("^(MCQ|PREDICT_STEP)$",
        ErrorMessage = "QuestionType must be 'MCQ' or 'PREDICT_STEP'.")]
    public string QuestionType { get; set; } = "MCQ";

    [Required]
    [StringLength(2000, MinimumLength = 5,
        ErrorMessage = "QuestionText must be between 5 and 2000 characters.")]
    public string QuestionText { get; set; } = string.Empty;

    [Required]
    [StringLength(500, ErrorMessage = "OptionA must not exceed 500 characters.")]
    public string OptionA { get; set; } = string.Empty;

    [Required]
    [StringLength(500, ErrorMessage = "OptionB must not exceed 500 characters.")]
    public string OptionB { get; set; } = string.Empty;

    [Required]
    [StringLength(500, ErrorMessage = "OptionC must not exceed 500 characters.")]
    public string OptionC { get; set; } = string.Empty;

    [Required]
    [StringLength(500, ErrorMessage = "OptionD must not exceed 500 characters.")]
    public string OptionD { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[ABCD]$",
        ErrorMessage = "CorrectOption must be a single letter: A, B, C, or D.")]
    public string CorrectOption { get; set; } = "A";

    [Required]
    [RegularExpression("^(easy|medium|hard)$",
        ErrorMessage = "Difficulty must be 'easy', 'medium', or 'hard'.")]
    public string Difficulty { get; set; } = "easy";

    [StringLength(2000, ErrorMessage = "Explanation must not exceed 2000 characters.")]
    public string? Explanation { get; set; }

    [Range(0, 9999, ErrorMessage = "OrderIndex must be between 0 and 9999.")]
    public int OrderIndex { get; set; }

    [Required]
    public bool IsActive { get; set; }
}
