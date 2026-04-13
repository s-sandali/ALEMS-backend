using System.Security.Claims;
using backend.DTOs;
using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Student-facing endpoints for account and profile information (read-only).
/// </summary>
/// <remarks>
/// All endpoints require a valid Clerk JWT (any authenticated user — Student or Admin).
/// </remarks>
[ApiController]
[Route("api/students")]
[Authorize]
[Produces("application/json")]
public class StudentController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly ILevelingService _levelingService;
    private readonly IActivityService _activityService;
    private readonly IStudentDashboardService _dashboardService;
    private readonly IActivityHeatmapService _heatmapService;
    private readonly ILogger<StudentController> _logger;

    public StudentController(
        IUserService userService,
        IUserRepository userRepository,
        ILevelingService levelingService,
        IActivityService activityService,
        IStudentDashboardService dashboardService,
        IActivityHeatmapService heatmapService,
        ILogger<StudentController> logger)
    {
        _userService = userService;
        _userRepository = userRepository;
        _levelingService = levelingService;
        _activityService = activityService;
        _dashboardService = dashboardService;
        _heatmapService = heatmapService;
        _logger = logger;
    }

    // ── GET /api/students/{id}/dashboard ──────────────────────────────

    /// <summary>
    /// GET /api/students/{id}/dashboard — Retrieve the student dashboard with XP, earned badges, and all available badges.
    /// </summary>
    /// <param name="id">The student user ID.</param>
    /// <returns>
    /// Dashboard containing:
    /// - StudentId: The student's user ID.
    /// - XpTotal: Total XP earned by the student.
    /// - EarnedBadges: List of earned badges with award dates and icons.
    /// - AllBadges: Complete list of all available badges with earned status for rendering.
    /// </returns>
    /// <response code="200">Dashboard retrieved successfully.</response>
    /// <response code="404">Student not found.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStudentDashboard(int id)
    {
        try
        {
            _logger.LogInformation("📊 GetStudentDashboard called for StudentId={StudentId}", id);

            var authorizationResult = await AuthorizeStudentAccessAsync(id);
            if (authorizationResult is not null)
                return authorizationResult;

            var dashboard = await _dashboardService.GetStudentDashboardAsync(id);

            if (dashboard is null)
            {
                _logger.LogWarning("❌ Student not found: {StudentId}", id);
                return NotFound(new
                {
                    status = "error",
                    message = $"Student with ID {id} not found."
                });
            }

            _logger.LogInformation("✅ Dashboard constructed: StudentId={StudentId}, EarnedBadges={EarnedCount}, AttemptHistory={HistoryCount}",
                id, dashboard.EarnedBadges.Count(), dashboard.QuizAttemptHistory.Count());

            return Ok(new
            {
                status = "success",
                data = dashboard
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error retrieving student dashboard for ID {StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving the dashboard."
            });
        }
    }

    // ── GET /api/students/{id}/progression ─────────────────────────────

    /// <summary>
    /// GET /api/students/{id}/progression — Retrieve the student's XP progression data.
    /// </summary>
    /// <param name="id">The student user ID.</param>
    /// <returns>
    /// Progression data containing:
    /// - UserId: The student's user ID.
    /// - XpTotal: Total XP earned by the student.
    /// - CurrentLevel: The student's current level.
    /// - XpPrevLevel: Cumulative XP required to reach current level.
    /// - XpForNextLevel: Cumulative XP required to reach next level.
    /// - XpInCurrentLevel: XP earned within the current level progression.
    /// - XpNeededForLevel: Total XP needed to advance to next level.
    /// - ProgressPercentage: Progress percentage (0-100) for current level.
    /// </returns>
    /// <response code="200">Progression data retrieved successfully.</response>
    /// <response code="404">Student not found.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}/progression")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserProgression(int id)
    {
        try
        {
            _logger.LogInformation("📈 GetUserProgression called for UserId={UserId}", id);

            var authorizationResult = await AuthorizeStudentAccessAsync(id);
            if (authorizationResult is not null)
                return authorizationResult;

            // Fetch the user
            var user = await _userService.GetUserByIdAsync(id);
            if (user is null)
            {
                _logger.LogWarning("❌ User not found: {UserId}", id);
                return NotFound(new
                {
                    status = "error",
                    message = $"User with ID {id} not found."
                });
            }

            _logger.LogInformation("✅ User found: {UserId}, XpTotal={XpTotal}", id, user.XpTotal);

            // Calculate progression data
            int currentLevel = _levelingService.CalculateLevel(user.XpTotal);
            int xpPrevLevel = _levelingService.GetXpForPreviousLevel(currentLevel);
            int xpForNextLevel = _levelingService.GetXpForNextLevel(currentLevel);
            int xpInCurrentLevel = user.XpTotal - xpPrevLevel;
            int xpNeededForLevel = xpForNextLevel - xpPrevLevel;
            double progressPercentage = xpNeededForLevel > 0 
                ? Math.Min((xpInCurrentLevel / (double)xpNeededForLevel) * 100, 100) 
                : 0;

            var progression = new UserProgressionDto
            {
                UserId = user.UserId,
                XpTotal = user.XpTotal,
                CurrentLevel = currentLevel,
                XpPrevLevel = xpPrevLevel,
                XpForNextLevel = xpForNextLevel,
                XpInCurrentLevel = xpInCurrentLevel,
                XpNeededForLevel = xpNeededForLevel,
                ProgressPercentage = progressPercentage
            };

            _logger.LogInformation(
                "✅ Progression calculated: Level={Level}, XpTotal={XpTotal}, Progress={Progress}%",
                currentLevel, user.XpTotal, Math.Round(progressPercentage));

            return Ok(new
            {
                status = "success",
                data = progression
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error retrieving user progression for ID {UserId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving progression data."
            });
        }
    }

    // ── GET /api/students/{id}/activity ───────────────────────────────

    /// <summary>
    /// GET /api/students/{id}/activity — Returns the student's most recent activity events
    /// (quiz completions and badge awards), ordered by date descending.
    /// </summary>
    /// <param name="id">The student user ID.</param>
    /// <param name="limit">Maximum number of events to return (default 10, max 50).</param>
    /// <response code="200">Activity list returned successfully.</response>
    /// <response code="400">Invalid limit parameter.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}/activity")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetRecentActivity(int id, [FromQuery] int limit = 10)
    {
        if (limit < 1 || limit > 50)
        {
            return BadRequest(new
            {
                status = "error",
                message = "limit must be between 1 and 50."
            });
        }

        try
        {
            _logger.LogInformation(
                "📋 GetRecentActivity called for StudentId={StudentId}, Limit={Limit}", id, limit);

            var authorizationResult = await AuthorizeStudentAccessAsync(id);
            if (authorizationResult is not null)
                return authorizationResult;

            var activity = await _activityService.GetRecentActivityAsync(id, limit);

            return Ok(new
            {
                status = "success",
                data = activity
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error retrieving recent activity for StudentId={StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving recent activity."
            });
        }
    }

    // ── GET /api/students/{id}/activity-heatmap ───────────────────────

    /// <summary>
    /// GET /api/students/{id}/activity-heatmap — Returns per-day quiz attempt counts
    /// for the student's contribution heatmap. Only completed attempts are counted.
    /// Days with no activity are omitted; the frontend fills the gaps.
    /// </summary>
    /// <param name="id">The student user ID.</param>
    /// <response code="200">Heatmap data returned successfully.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet("{id:int}/activity-heatmap")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActivityHeatmap(int id)
    {
        try
        {
            _logger.LogInformation(
                "🗓️ GetActivityHeatmap called for StudentId={StudentId}", id);

            var authorizationResult = await AuthorizeStudentAccessAsync(id);
            if (authorizationResult is not null)
                return authorizationResult;

            var heatmap = await _heatmapService.GetDailyActivityAsync(id);

            return Ok(new
            {
                status = "success",
                data = heatmap
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error retrieving activity heatmap for StudentId={StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving the activity heatmap."
            });
        }
    }

    private async Task<IActionResult?> AuthorizeStudentAccessAsync(int requestedUserId)
    {
        if (User.IsInRole("Admin"))
            return null;

        var clerkUserId = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(clerkUserId))
        {
            _logger.LogWarning("❌ Student endpoint access denied: missing authenticated Clerk user id claim.");
            return Unauthorized(new
            {
                status = "error",
                message = "Unable to identify the authenticated user."
            });
        }

        var currentUser = await _userRepository.GetByClerkUserIdAsync(clerkUserId);
        if (currentUser is null)
        {
            _logger.LogWarning(
                "❌ Student endpoint access denied: no local user found for ClerkId={ClerkUserId}",
                clerkUserId);
            return NotFound(new
            {
                status = "error",
                message = "User account not found."
            });
        }

        if (currentUser.UserId != requestedUserId)
        {
            _logger.LogWarning(
                "❌ Unauthorized student access attempt: ClerkId={ClerkUserId}, LocalUserId={LocalUserId}, RequestedUserId={RequestedUserId}",
                clerkUserId, currentUser.UserId, requestedUserId);
            return Forbid();
        }

        return null;
    }
}
