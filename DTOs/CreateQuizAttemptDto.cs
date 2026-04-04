using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request DTO for submitting a quiz attempt.
/// </summary>
public class CreateQuizAttemptDto
{
    [Required(ErrorMessage = "Answers are required.")]
    [MinLength(1, ErrorMessage = "At least one answer is required.")]
    public List<QuizAttemptAnswerSubmissionDto> Answers { get; set; } = [];
}
