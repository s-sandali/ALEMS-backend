namespace backend.DTOs;

/// <summary>
/// Describes a language supported by the code execution service.
/// </summary>
public class SupportedLanguageDto
{
    public int    LanguageId { get; set; }
    public string Name       { get; set; } = string.Empty;
}
