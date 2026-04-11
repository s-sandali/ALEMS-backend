namespace backend.DTOs;

/// <summary>
/// DTO for earned badges returned in dashboard endpoints.
/// Includes badge info with ID field matching frontend expectations.
/// </summary>
public class EarnedBadgeDto
{
    public int Id { get; set; }  // BadgeId - frontend expects 'id'
    public string Name { get; set; } = string.Empty;  // BadgeName - frontend expects 'name'
    public string Icon { get; set; } = string.Empty;  // Badge icon emoji/unicode
    public DateTime AwardDate { get; set; }  // AwardedAt - frontend expects 'awardDate'
}
