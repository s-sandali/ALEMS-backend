namespace backend.DTOs;

/// <summary>
/// Response DTO returned from algorithm endpoints.
/// </summary>
public class AlgorithmResponseDto
{
    public int AlgorithmId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeComplexityBest { get; set; } = string.Empty;
    public string TimeComplexityAverage { get; set; } = string.Empty;
    public string TimeComplexityWorst { get; set; } = string.Empty;
    public bool QuizAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}
