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
}
