namespace backend.DTOs;

/// <summary>
/// Response DTO returned from coding question endpoints.
/// </summary>
public class CodingQuestionResponseDto
{
    public int     Id             { get; set; }
    public string  Title          { get; set; } = string.Empty;
    public string  Description    { get; set; } = string.Empty;
    public string? InputExample   { get; set; }
    public string? ExpectedOutput { get; set; }
    public string  Difficulty     { get; set; } = string.Empty;
}
