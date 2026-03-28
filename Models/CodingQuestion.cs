namespace backend.Models;

/// <summary>
/// Domain model representing a row in the coding_questions table.
/// </summary>
public class CodingQuestion
{
    public int    Id             { get; set; }
    public string Title          { get; set; } = string.Empty;
    public string Description    { get; set; } = string.Empty;
    public string? InputExample  { get; set; }
    public string? ExpectedOutput { get; set; }
    public string Difficulty     { get; set; } = "easy"; // "easy" | "medium" | "hard"
}
