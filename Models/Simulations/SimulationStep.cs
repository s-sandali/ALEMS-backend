using System.Text.Json.Serialization;

namespace backend.Models.Simulations;

/// <summary>
/// Represents a single step in an algorithm simulation.
/// </summary>
public class SimulationStep
{
    public int StepNumber { get; set; }

    public int[] ArrayState { get; set; } = [];

    public int[] ActiveIndices { get; set; } = [];

    public int LineNumber { get; set; }

    public string ActionLabel { get; set; } = string.Empty;

    /// <summary>
    /// Operation type for richer visualization (e.g., compare, swap, pivotPlaced, pivot_select)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    /// <summary>
    /// Index of the pivot element (used in partitioning algorithms)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PivotIndex { get; set; }

    /// <summary>
    /// Current sub-array range [low, high] being processed
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? Range { get; set; }

    /// <summary>
    /// Recursion depth in the call stack (for recursive algorithms)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RecursionDepth { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SearchStepModel? Search { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HeapStepModel? Heap { get; set; }
}
