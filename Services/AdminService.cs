using backend.DTOs;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles admin-level platform statistics and aggregations.
/// </summary>
public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly IQuizRepository _quizRepository;
    private readonly IQuizAttemptRepository _attemptRepository;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IUserRepository userRepository,
        IQuizRepository quizRepository,
        IQuizAttemptRepository attemptRepository,
        ILogger<AdminService> logger)
    {
        _userRepository = userRepository;
        _quizRepository = quizRepository;
        _attemptRepository = attemptRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminStatsDto> GetPlatformStatsAsync()
    {
        try
        {
            // Fetch total users
            var allUsers = await _userRepository.GetAllAsync();
            var totalUsers = allUsers.Count();

            // Fetch total quizzes
            var allQuizzes = await _quizRepository.GetAllAsync();
            var totalQuizzes = allQuizzes.Count();

            // Fetch all attempts to calculate totals and pass rate
            var allAttempts = await _attemptRepository.GetAllAsync();
            var totalAttempts = allAttempts.Count();

            // Calculate average pass rate
            // If no attempts exist, default to 0.0
            var averagePassRate = 0.0;
            if (totalAttempts > 0)
            {
                var passedAttempts = allAttempts.Count(a => a.Passed);
                averagePassRate = (passedAttempts * 100.0) / totalAttempts;
            }

            _logger.LogInformation(
                "Platform stats retrieved: Users={TotalUsers}, Quizzes={TotalQuizzes}, " +
                "Attempts={TotalAttempts}, AvgPassRate={AvgPassRate:F2}%",
                totalUsers, totalQuizzes, totalAttempts, averagePassRate);

            return new AdminStatsDto
            {
                TotalUsers = totalUsers,
                TotalQuizzes = totalQuizzes,
                TotalAttempts = totalAttempts,
                AveragePassRate = averagePassRate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving platform stats");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync()
    {
        try
        {
            // Fetch all users and all quiz attempts in parallel
            var usersTask = _userRepository.GetAllAsync();
            var attemptsTask = _attemptRepository.GetAllAsync();

            await Task.WhenAll(usersTask, attemptsTask);

            var users = await usersTask;
            var attempts = await attemptsTask;

            // Group attempts by user and calculate statistics
            var userAttemptStats = attempts
                .GroupBy(a => a.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => new
                    {
                        AttemptCount = g.Count(),
                        AverageScore = g.Count() > 0 ? g.Average(a => a.Score) : 0.0
                    }
                );

            // Build leaderboard entries and sort by XpTotal descending
            var leaderboardEntries = users
                .Select(user => new LeaderboardEntryDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    XpTotal = user.XpTotal,
                    AttemptCount = userAttemptStats.ContainsKey(user.UserId)
                        ? userAttemptStats[user.UserId].AttemptCount
                        : 0,
                    AverageScore = userAttemptStats.ContainsKey(user.UserId)
                        ? userAttemptStats[user.UserId].AverageScore
                        : 0.0
                })
                .OrderByDescending(e => e.XpTotal)
                .ToList();

            // Assign ranks (1-based) accounting for ties
            int rank = 1;
            for (int i = 0; i < leaderboardEntries.Count; i++)
            {
                if (i > 0 && leaderboardEntries[i].XpTotal < leaderboardEntries[i - 1].XpTotal)
                {
                    rank = i + 1;
                }
                leaderboardEntries[i].Rank = rank;
            }

            _logger.LogInformation("Leaderboard retrieved with {Count} entries", leaderboardEntries.Count);

            return leaderboardEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leaderboard");
            throw;
        }
    }
}
