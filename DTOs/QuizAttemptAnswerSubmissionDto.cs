using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Represents one submitted answer within a quiz attempt request.
/// </summary>
public class QuizAttemptAnswerSubmissionDto
{
    [Range(1, int.MaxValue, ErrorMessage = "QuestionId must be a positive integer.")]
    public int QuestionId { get; set; }

    [Required(ErrorMessage = "SelectedOption is required.")]
    [RegularExpression("^[ABCD]$", ErrorMessage = "SelectedOption must be one of: A, B, C, or D.")]
    public string SelectedOption { get; set; } = string.Empty;
}
