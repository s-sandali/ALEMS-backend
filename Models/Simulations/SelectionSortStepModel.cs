namespace backend.Models.Simulations;

/// <summary>
/// Selection Sort-specific metadata for a simulation step.
/// </summary>
public class SelectionSortStepModel
{
    public string Type { get; set; } = string.Empty;

    public int? CurrentIndex { get; set; }

    public int? CandidateIndex { get; set; }

    public int? MinIndex { get; set; }

    public int? SwapFrom { get; set; }

    public int? SwapTo { get; set; }

    public int? SortedBoundary { get; set; }
}
