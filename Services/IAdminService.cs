using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Handles admin-level platform statistics and aggregations.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Retrieves platform-wide statistics including total users, total quizzes,
    /// total attempts, and average pass rate across all attempts.
    /// </summary>
    Task<AdminStatsDto> GetPlatformStatsAsync();

    /// <summary>
    /// Retrieves a leaderboard of users ranked by total XP in descending order.
    /// Each entry includes attempt count and average quiz score per user.
    /// </summary>
    /// <returns>
    /// Collection of leaderboard entries sorted by XpTotal descending, with rank assigned sequentially starting from 1.
    /// </returns>
    Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync();
}
