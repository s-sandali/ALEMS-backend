namespace backend.DTOs;

/// <summary>
/// DTO for earned badges returned in dashboard endpoints.
/// Includes badge info with styling properties for UI rendering.
/// </summary>
public class EarnedBadgeDto
{
    public int Id { get; set; }  // BadgeId - frontend expects 'id'
    public string Name { get; set; } = string.Empty;  // BadgeName - frontend expects 'name'
    public string Description { get; set; } = string.Empty;  // BadgeDescription
    public int XpThreshold { get; set; }  // XP required to earn
    public string IconType { get; set; } = "star";  // lucide-react icon type
    public string IconColor { get; set; } = "#8f8f3e";  // Icon color in hex
    public DateTime AwardDate { get; set; }  // AwardedAt - frontend expects 'awardDate'
}
