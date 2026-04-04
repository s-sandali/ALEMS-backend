using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request DTO for updating an existing coding question (PUT /api/coding-questions/{id}).
/// </summary>
public class UpdateCodingQuestionDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(255, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 255 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; set; } = string.Empty;

    public string? InputExample { get; set; }

    public string? ExpectedOutput { get; set; }

    [Required(ErrorMessage = "Difficulty is required.")]
    [RegularExpression("^(easy|medium|hard)$", ErrorMessage = "Difficulty must be 'easy', 'medium', or 'hard'.")]
    public string Difficulty { get; set; } = "easy";
}
