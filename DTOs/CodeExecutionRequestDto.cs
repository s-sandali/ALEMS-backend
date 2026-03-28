using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

/// <summary>
/// Request DTO for executing code (POST /api/code/execute).
/// </summary>
public class CodeExecutionRequestDto
{
    [Required(ErrorMessage = "SourceCode is required.")]
    public string SourceCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "LanguageId is required.")]
    [Range(1, 9999, ErrorMessage = "LanguageId must be a positive integer.")]
    public int LanguageId { get; set; }

    /// <summary>Standard input to pass to the program (optional).</summary>
    public string? Stdin { get; set; }
}
