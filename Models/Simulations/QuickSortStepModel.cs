namespace backend.Models.Simulations;

/// <summary>
/// Quick Sort-specific metadata for a simulation step.
/// </summary>
public class QuickSortStepModel
{
    public string Type { get; set; } = string.Empty;

    public int? Pivot { get; set; }

    public int? PivotIndex { get; set; }

    public int[] Range { get; set; } = [];

    public int? RecursionDepth { get; set; }
}