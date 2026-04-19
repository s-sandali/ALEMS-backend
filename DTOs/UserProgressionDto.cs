namespace backend.DTOs;

/// <summary>
/// DTO for user progression data including current level and XP thresholds.
/// Used to populate frontend XP progress bar component.
/// </summary>
public class UserProgressionDto
{
    /// <summary>
    /// The user's ID.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Total XP earned by the user across all activities.
    /// </summary>
    public int XpTotal { get; set; }

    /// <summary>
    /// The user's current level.
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// Cumulative XP required to reach the current level (start of current level progression).
    /// </summary>
    public int XpPrevLevel { get; set; }

    /// <summary>
    /// Cumulative XP required to reach the next level (goal for current progression).
    /// </summary>
    public int XpForNextLevel { get; set; }

    /// <summary>
    /// XP progress within current level (XpTotal - XpPrevLevel).
    /// </summary>
    public int XpInCurrentLevel { get; set; }

    /// <summary>
    /// Total XP needed to advance to next level (XpForNextLevel - XpPrevLevel).
    /// </summary>
    public int XpNeededForLevel { get; set; }

    /// <summary>
    /// Progress percentage for current level (0-100).
    /// </summary>
    public double ProgressPercentage { get; set; }
}
