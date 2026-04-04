using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request DTO for creating a new quiz (POST /api/quizzes).
/// </summary>
public class CreateQuizDto
{
    [Required(ErrorMessage = "AlgorithmId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "AlgorithmId must be a positive integer.")]
    public int AlgorithmId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 255 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description must not exceed 2000 characters.")]
    public string? Description { get; set; }

    [Range(1, 300, ErrorMessage = "TimeLimitMins must be between 1 and 300.")]
    public int? TimeLimitMins { get; set; }

    [Range(0, 100, ErrorMessage = "PassScore must be between 0 and 100.")]
    public int PassScore { get; set; } = 70;
}
