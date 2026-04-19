using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Builds the leaderboard for a given authenticated user.
/// </summary>
public interface ILeaderboardService
{
    /// <summary>
    /// Returns the top <paramref name="limit"/> users by XP, each annotated with their rank.
    /// If the authenticated user (<paramref name="currentUserId"/>) does not appear in the top
    /// list, they are appended at the end with their actual rank so the frontend can always
    /// show the current user's position.
    /// </summary>
    /// <param name="currentUserId">Internal user ID of the authenticated caller.</param>
    /// <param name="limit">Maximum number of top-ranked entries to return (default 10).</param>
    Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync(int currentUserId, int limit = 10);
}
