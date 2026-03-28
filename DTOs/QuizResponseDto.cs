namespace backend.DTOs;

/// <summary>
/// Response DTO returned from quiz endpoints.
/// </summary>
public class QuizResponseDto
{
    public int QuizId { get; set; }
    public int AlgorithmId { get; set; }
    public int CreatedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? TimeLimitMins { get; set; }
    public int PassScore { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
