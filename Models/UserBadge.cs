namespace backend.Models;

/// <summary>
/// Domain model representing a row in the user_badges table.
/// Tracks badge awards for users.
/// </summary>
public class UserBadge
{
    public int UserBadgeId { get; set; }
    public int UserId { get; set; }
    public int BadgeId { get; set; }
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User? User { get; set; }
    public Badge? Badge { get; set; }
}
