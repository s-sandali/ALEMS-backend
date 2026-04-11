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
}
