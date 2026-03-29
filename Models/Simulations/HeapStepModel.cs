namespace backend.Models.Simulations;

/// <summary>
/// Heap-specific metadata for a simulation step.
/// </summary>
public class HeapStepModel
{
    public string Phase { get; set; } = string.Empty;

    public int HeapBoundaryEnd { get; set; }

    public int? HeapIndex { get; set; }

    public int? ParentIndex { get; set; }

    public int? LeftChildIndex { get; set; }

    public int? RightChildIndex { get; set; }

    public int? ComparedParentIndex { get; set; }

    public int? ComparedChildIndex { get; set; }

    public int[] ComparedIndices { get; set; } = [];

    public string? ParentChildComparison { get; set; }
}