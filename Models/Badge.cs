namespace backend.Models;

/// <summary>
/// Domain model representing a row in the badges table.
/// Stores badge definitions with XP thresholds and UI styling properties.
/// </summary>
public class Badge
{
    public int BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string BadgeDescription { get; set; } = string.Empty;
    public int XpThreshold { get; set; }
    public string IconType { get; set; } = "star";  // Maps to lucide-react icons: star, bolt, shield, etc.
    public string IconColor { get; set; } = "#8f8f3e";  // Icon color in hex format (default lime green)
    public string UnlockHint { get; set; } = "Locked";  // Hint text for locked badges
    public int? AlgorithmId { get; set; }  // NULL = XP badge; set = algorithm badge
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
