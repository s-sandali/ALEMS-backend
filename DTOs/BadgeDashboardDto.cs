namespace backend.DTOs;

/// <summary>
/// DTO for displayed badges in student dashboard.
/// Includes earned status and icon for UI rendering.
/// </summary>
public class BadgeDashboardDto
{
    public int Id { get; set; }  // BadgeId - frontend expects 'id'
    public string Name { get; set; } = string.Empty;  // BadgeName - frontend expects 'name'
    public string Icon { get; set; } = string.Empty;  // Badge icon emoji/unicode
    public bool Earned { get; set; }  // Whether this badge has been earned by the student
}
