namespace backend.DTOs;

/// <summary>
/// Represents a single recent-activity event for a student.
/// The <see cref="Type"/> field discriminates between quiz completions and badge awards.
/// </summary>
public class ActivityItemDto
{
    /// <summary>quiz | badge</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Quiz title or badge name.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>XP earned from this event (0 for badge events).</summary>
    public int XpEarned { get; set; }

    /// <summary>When the event occurred (quiz completion or badge award timestamp).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Optional extra data (reserved for future use).</summary>
    public string? Metadata { get; set; }
}
