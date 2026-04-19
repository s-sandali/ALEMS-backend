namespace backend.DTOs;

/// <summary>
/// Represents a single day's quiz activity count for the contribution heatmap.
/// </summary>
public class ActivityHeatmapDto
{
    /// <summary>
    /// The calendar date (UTC, time component is always midnight).
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of quiz attempts completed on this date.
    /// </summary>
    public int Count { get; set; }
}
