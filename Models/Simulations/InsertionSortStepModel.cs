namespace backend.Models.Simulations;

/// <summary>
/// Insertion Sort-specific metadata for a simulation step.
/// </summary>
public class InsertionSortStepModel
{
    public string Type { get; set; } = string.Empty;

    public int? CurrentIndex { get; set; }

    public int? Key { get; set; }

    public int? CompareIndex { get; set; }

    public int? ShiftFrom { get; set; }

    public int? ShiftTo { get; set; }

    public int? InsertPosition { get; set; }

    public int? SortedBoundary { get; set; }
}
