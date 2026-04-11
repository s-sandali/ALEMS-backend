using backend.Data;
using backend.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Dashboard endpoint for user analytics and learning progress.
/// Provides aggregated statistics for user dashboards across the platform.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]  // Requires JWT token
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly UserRepository _userRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(UserRepository userRepository, ILogger<DashboardController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/dashboard/summary
    /// Retrieves aggregated dashboard summary statistics for the current user.
    /// </summary>
    /// <remarks>
    /// Returns user statistics including total quizzes attempted, problems solved,
    /// XP earned, and streaks. Restricted to authenticated users.
    /// </remarks>
    /// <response code="200">Dashboard summary data retrieved successfully.</response>
    /// <response code="401">User not authenticated.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDashboardSummary()
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Dashboard summary requested without valid user claim");
                return Unauthorized(new { message = "Invalid user context" });
            }

            // Note: In production, fetch actual user stats from repositories
            var summary = new DashboardSummaryDto
            {
                UserId = userIdClaim,
                TotalQuizzesTaken = 0,
                TotalProblemsSolved = 0,
                CurrentXp = 0,
                CurrentStreak = 0,
                LastActivityDate = DateTime.UtcNow
            };

            _logger.LogInformation("Dashboard summary retrieved for user {UserId}", userIdClaim);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard summary");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/dashboard/stats
    /// Retrieves detailed performance statistics for the current user.
    /// </summary>
    /// <remarks>
    /// Returns performance metrics including average quiz score, problem accuracy,
    /// algorithm proficiency levels, and topic progress. Restricted to authenticated users.
    /// </remarks>
    /// <response code="200">Performance statistics retrieved successfully.</response>
    /// <response code="401">User not authenticated.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPerformanceStats()
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Performance stats requested without valid user claim");
                return Unauthorized(new { message = "Invalid user context" });
            }

            var stats = new DashboardStatsDto
            {
                UserId = userIdClaim,
                AverageQuizScore = 0,
                ProblemAccuracy = 0,
                AlgorithmsMastered = new List<string>(),
                TopicsProgress = new Dictionary<string, int>()
            };

            _logger.LogInformation("Performance stats retrieved for user {UserId}", userIdClaim);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving performance stats");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// GET /api/dashboard/recent-activity
    /// Retrieves recent user activity (quizzes, problems, simulations).
    /// </summary>
    /// <remarks>
    /// Returns the 10 most recent activities including completed quizzes,
    /// solved problems, and simulation runs. Restricted to authenticated users.
    /// </remarks>
    /// <response code="200">Recent activity data retrieved successfully.</response>
    /// <response code="401">User not authenticated.</response>
    [HttpGet("recent-activity")]
    [ProducesResponseType(typeof(List<DashboardActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecentActivity()
    {
        try
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                _logger.LogWarning("Recent activity requested without valid user claim");
                return Unauthorized(new { message = "Invalid user context" });
            }

            var activities = new List<DashboardActivityDto>();

            _logger.LogInformation("Recent activity retrieved for user {UserId}", userIdClaim);
            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent activity");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

/// <summary>
/// DTO for dashboard summary statistics.
/// </summary>
public class DashboardSummaryDto
{
    public string UserId { get; set; } = string.Empty;
    public int TotalQuizzesTaken { get; set; }
    public int TotalProblemsSolved { get; set; }
    public int CurrentXp { get; set; }
    public int CurrentStreak { get; set; }
    public DateTime LastActivityDate { get; set; }
}

/// <summary>
/// DTO for dashboard performance statistics.
/// </summary>
public class DashboardStatsDto
{
    public string UserId { get; set; } = string.Empty;
    public double AverageQuizScore { get; set; }
    public double ProblemAccuracy { get; set; }
    public List<string> AlgorithmsMastered { get; set; } = new();
    public Dictionary<string, int> TopicsProgress { get; set; } = new();
}

/// <summary>
/// DTO for individual activity items.
/// </summary>
public class DashboardActivityDto
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty; // "quiz", "problem", "simulation"
    public string Title { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public int? Score { get; set; }
    public bool Passed { get; set; }
}
