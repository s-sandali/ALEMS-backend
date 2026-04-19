namespace backend.DTOs;

/// <summary>
/// Represents a single user entry in the admin leaderboard.
/// </summary>
public class LeaderboardEntryDto
{
    /// <summary>
    /// Internal auto-increment user ID.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Public display name of the user.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Total XP earned by the user, used for ranking.
    /// </summary>
    public int XpTotal { get; set; }

    /// <summary>
    /// Total number of quiz attempts made by the user.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Average score across all quiz attempts.
    /// Value is 0 if user has no attempts.
    /// </summary>
    public double AverageScore { get; set; }

    /// <summary>
    /// 1-based rank position ordered by XpTotal descending.
    /// Users with the same XP receive different consecutive ranks (competition ranking).
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// True when this entry belongs to the authenticated user making the request.
    /// Used by the frontend to highlight the current user's row.
    /// </summary>
    public bool IsCurrentUser { get; set; }
}
