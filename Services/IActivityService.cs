using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Provides recent-activity aggregation for a student across quiz completions and badge awards.
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// Returns the most recent activity events for the given student, ordered by date descending.
    /// </summary>
    /// <param name="userId">Internal auto-increment user ID.</param>
    /// <param name="limit">Maximum number of events to return (defaults to 10).</param>
    Task<IEnumerable<ActivityItemDto>> GetRecentActivityAsync(int userId, int limit = 10);
}
