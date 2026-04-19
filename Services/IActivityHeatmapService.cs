using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Provides per-day quiz activity counts for the dashboard heatmap.
/// </summary>
public interface IActivityHeatmapService
{
    /// <summary>
    /// Returns one entry per calendar day on which the student completed at least one quiz attempt.
    /// Days with no activity are omitted; the frontend fills the gaps visually.
    /// </summary>
    /// <param name="userId">Internal auto-increment user ID.</param>
    Task<IEnumerable<ActivityHeatmapDto>> GetDailyActivityAsync(int userId);
}
