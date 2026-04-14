using backend.DTOs;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Delegates recent-activity aggregation to <see cref="IQuizAttemptRepository"/>,
/// which issues a single UNION ALL query across quiz_attempts and user_badges.
/// </summary>
public class ActivityService : IActivityService
{
    private readonly IQuizAttemptRepository _quizAttemptRepository;

    public ActivityService(IQuizAttemptRepository quizAttemptRepository)
    {
        _quizAttemptRepository = quizAttemptRepository;
    }

    /// <inheritdoc />
    public Task<IEnumerable<ActivityItemDto>> GetRecentActivityAsync(int userId, int limit = 10)
    {
        return _quizAttemptRepository.GetRecentActivityAsync(userId, limit);
    }
}
