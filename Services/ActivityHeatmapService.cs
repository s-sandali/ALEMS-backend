using backend.DTOs;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Delegates heatmap data retrieval to <see cref="IQuizAttemptRepository"/>.
/// </summary>
public class ActivityHeatmapService : IActivityHeatmapService
{
    private readonly IQuizAttemptRepository _quizAttemptRepository;

    public ActivityHeatmapService(IQuizAttemptRepository quizAttemptRepository)
    {
        _quizAttemptRepository = quizAttemptRepository;
    }

    /// <inheritdoc />
    public Task<IEnumerable<ActivityHeatmapDto>> GetDailyActivityAsync(int userId)
        => _quizAttemptRepository.GetDailyActivityAsync(userId);
}
