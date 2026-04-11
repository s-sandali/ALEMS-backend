namespace backend.DTOs;

/// <summary>
/// DTO for the student dashboard response.
/// Contains student ID, XP total, earned badges with award dates, and all badges for locked placeholders.
/// Field names match frontend expectations.
/// </summary>
public class StudentDashboardDto
{
    /// <summary>
    /// The student's user ID.
    /// </summary>
    public int StudentId { get; set; }

    /// <summary>
    /// Total XP earned by the student.
    /// </summary>
    public int XpTotal { get; set; }

    /// <summary>
    /// List of badges earned by the student, including award dates and icon.
    /// Sorted by badge unlock threshold ascending.
    /// </summary>
    public IEnumerable<EarnedBadgeDto> EarnedBadges { get; set; } = new List<EarnedBadgeDto>();

    /// <summary>
    /// List of all available badges including earned status.
    /// Used for rendering earned badges fully and locked badges as grayscale placeholders.
    /// Sorted by unlock threshold ascending.
    /// </summary>
    public IEnumerable<BadgeDashboardDto> AllBadges { get; set; } = new List<BadgeDashboardDto>();
}
