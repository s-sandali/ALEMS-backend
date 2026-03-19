namespace backend.Models;

/// <summary>
/// Domain model representing a row in the algorithms table.
/// </summary>
public class Algorithm
{
    public int AlgorithmId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TimeComplexityBest { get; set; } = string.Empty;
    public string TimeComplexityAverage { get; set; } = string.Empty;
    public string TimeComplexityWorst { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
