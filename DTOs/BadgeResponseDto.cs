namespace backend.DTOs;

/// <summary>
/// DTO for badge responses in API endpoints.
/// </summary>
public class BadgeResponseDto
{
    public int BadgeId { get; set; }
    public string BadgeName { get; set; } = string.Empty;
    public string BadgeDescription { get; set; } = string.Empty;
    public int XpThreshold { get; set; }
}
