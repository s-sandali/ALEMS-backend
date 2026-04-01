using System.Text.Json.Serialization;

namespace backend.Models.Simulations;

/// <summary>
/// Merge Sort-specific metadata attached to each simulation step.
/// </summary>
public class MergeSortStepModel
{
    /// <summary>
    /// Step type, e.g. "split", "merge_start", "compare", "place", "merge_complete", "return".
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Left boundary of the current sub-array (inclusive).</summary>
    public int Left { get; set; }

    /// <summary>Right boundary of the current sub-array (inclusive).</summary>
    public int Right { get; set; }

    /// <summary>Midpoint index used to split (null for leaf / merge steps).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Mid { get; set; }

    /// <summary>Recursion depth of the current call frame.</summary>
    public int RecursionDepth { get; set; }

    /// <summary>
    /// The temporary merged sub-array being built during a merge pass.
    /// Null when not in a merge phase.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int[]? MergeBuffer { get; set; }

    /// <summary>
    /// Index within the main array where the next merged value will be placed.
    /// Null when not in a place phase.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PlaceIndex { get; set; }
}
