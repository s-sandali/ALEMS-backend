namespace backend.Models.Simulations;

/// <summary>
/// Search-specific metadata for a simulation step.
/// </summary>
public class SearchStepModel
{
    public int LowIndex { get; set; }

    public int HighIndex { get; set; }

    public int? MidpointIndex { get; set; }

    public string State { get; set; } = string.Empty;

    public string? DiscardedSide { get; set; }

    public int? DiscardStartIndex { get; set; }

    public int? DiscardEndIndex { get; set; }

    public int[] DiscardedIndices { get; set; } = [];
}