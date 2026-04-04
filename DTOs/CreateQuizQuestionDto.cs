using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Payload for creating a new quiz question.
/// Supports both MCQ and PREDICT_STEP question types —
/// both are stored identically (4 options, one correct answer).
/// </summary>
public class CreateQuizQuestionDto
{
    /// <summary>
    /// Question type: "MCQ" (knowledge check) or "PREDICT_STEP" (algorithm step prediction).
    /// </summary>
    [Required]
    [RegularExpression("^(MCQ|PREDICT_STEP)$",
        ErrorMessage = "QuestionType must be 'MCQ' or 'PREDICT_STEP'.")]
    public string QuestionType { get; set; } = "MCQ";

    /// <summary>
    /// The question text shown to the student.
    /// </summary>
    [Required]
    [StringLength(2000, MinimumLength = 5,
        ErrorMessage = "QuestionText must be between 5 and 2000 characters.")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Option A text.</summary>
    [Required]
    [StringLength(500, ErrorMessage = "OptionA must not exceed 500 characters.")]
    public string OptionA { get; set; } = string.Empty;

    /// <summary>Option B text.</summary>
    [Required]
    [StringLength(500, ErrorMessage = "OptionB must not exceed 500 characters.")]
    public string OptionB { get; set; } = string.Empty;

    /// <summary>Option C text.</summary>
    [Required]
    [StringLength(500, ErrorMessage = "OptionC must not exceed 500 characters.")]
    public string OptionC { get; set; } = string.Empty;

    /// <summary>Option D text.</summary>
    [Required]
    [StringLength(500, ErrorMessage = "OptionD must not exceed 500 characters.")]
    public string OptionD { get; set; } = string.Empty;

    /// <summary>
    /// The letter of the correct option: "A", "B", "C", or "D".
    /// </summary>
    [Required]
    [RegularExpression("^[ABCD]$",
        ErrorMessage = "CorrectOption must be a single letter: A, B, C, or D.")]
    public string CorrectOption { get; set; } = "A";

    /// <summary>
    /// Difficulty tier: "easy", "medium", or "hard".
    /// </summary>
    [Required]
    [RegularExpression("^(easy|medium|hard)$",
        ErrorMessage = "Difficulty must be 'easy', 'medium', or 'hard'.")]
    public string Difficulty { get; set; } = "easy";

    /// <summary>
    /// Optional explanation shown to the student after they submit.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Explanation must not exceed 2000 characters.")]
    public string? Explanation { get; set; }

    /// <summary>
    /// Display order within the quiz (0-based). Lower values appear first.
    /// </summary>
    [Range(0, 9999, ErrorMessage = "OrderIndex must be between 0 and 9999.")]
    public int OrderIndex { get; set; }
}
