namespace backend.Models.Simulations;

/// <summary>
/// Captures Bubble Sort partition boundaries for each simulation step.
/// </summary>
public class BubbleStepModel
{
    public int PassNumber { get; set; }

    public int SortedStartIndex { get; set; }

    public int SortedEndIndex { get; set; }

    public int UnsortedStartIndex { get; set; }

    public int UnsortedEndIndex { get; set; }

    public string Phase { get; set; } = string.Empty;
}