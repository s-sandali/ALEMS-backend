namespace backend.DTOs;

/// <summary>
/// Represents a single user entry in the admin leaderboard.
/// </summary>
public class LeaderboardEntryDto
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Username of the user.
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
    /// The rank of the user on the leaderboard (1-based).
    /// </summary>
    public int Rank { get; set; }
}
