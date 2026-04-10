namespace backend.Models;

/// <summary>
/// Domain model representing a row in the badges table.
/// Stores badge definitions with XP thresholds.
/// </summary>
public class Badge
{
    public int BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string BadgeDescription { get; set; } = string.Empty;
    public int XpThreshold { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
