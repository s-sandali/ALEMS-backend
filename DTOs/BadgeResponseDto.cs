namespace backend.DTOs;

/// <summary>
/// DTO for badge responses in API endpoints.
/// Includes UI styling properties for rich frontend rendering.
/// </summary>
public class BadgeResponseDto
{
    public int BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string BadgeDescription { get; set; } = string.Empty;
    public int XpThreshold { get; set; }
    public string IconType { get; set; } = "star";  // lucide-react icon type
    public string IconColor { get; set; } = "#8f8f3e";  // Icon color in hex
    public string UnlockHint { get; set; } = "Locked";  // Hint text for locked state
}
