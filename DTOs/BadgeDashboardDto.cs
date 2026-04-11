namespace backend.DTOs;

/// <summary>
/// DTO for displayed badges in student dashboard.
/// Includes styling and earning status for UI rendering.
/// </summary>
public class BadgeDashboardDto
{
    public int Id { get; set; }  // BadgeId - frontend expects 'id'
    public string Name { get; set; } = string.Empty;  // BadgeName - frontend expects 'name'
    public string Description { get; set; } = string.Empty;  // BadgeDescription
    public int XpThreshold { get; set; }  // XP required to earn badge
    public string IconType { get; set; } = "star";  // lucide-react icon type
    public string IconColor { get; set; } = "#8f8f3e";  // Icon color in hex
    public string UnlockHint { get; set; } = "Locked";  // Copy for locked badge UI
    public bool Earned { get; set; }  // Whether this badge has been earned by the student
}
