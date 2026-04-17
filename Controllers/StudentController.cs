using System.Security.Claims;
using backend.DTOs;
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
    private readonly IBadgeService _badgeService;
    private readonly ILevelingService _levelingService;
    private readonly IQuizAttemptService _attemptService;
    private readonly ILogger<StudentController> _logger;

    public StudentController(
        IUserService userService,
        IBadgeService badgeService,
        ILevelingService levelingService,
        IQuizAttemptService attemptService,
        ILogger<StudentController> logger)
    {
        _userService = userService;
        _badgeService = badgeService;
        _levelingService = levelingService;
        _attemptService = attemptService;
        _logger = logger;
    }

    // ── GET /api/students/{id}/dashboard ──────────────────────────────

    /// <summary>
    /// GET /api/students/{id}/dashboard — Retrieve the student dashboard with XP, earned badges, and all available badges.
    /// </summary>
    /// <remarks>
    /// Returns comprehensive dashboard data including:
    /// - StudentId: The student's user ID
    /// - XpTotal: Total experience points earned
    /// - EarnedBadges: Array of badges earned with award dates and styling (icons, colors)
    /// - AllBadges: Complete badge catalog with earned status indicator for UI rendering
    /// 
    /// **Authorization**: Student may view own dashboard; Admin may view any student.
    /// </remarks>
    /// <param name="id">The student user ID (path parameter). Example: 238</param>
    /// <returns>
    /// Success response with StudentDashboard object containing xpTotal (number), 
    /// earnedBadges (array of {id, name, description, xpThreshold, earned, iconType, iconColor}),
    /// and allBadges (array of same structure with earned boolean flag).
    /// </returns>
    /// <response code="200">Dashboard retrieved successfully with all badge data.</response>
    /// <response code="403">Unauthorized - insufficient permissions to view this student's dashboard.</response>
    /// <response code="404">Student not found with the given ID.</response>
    /// <response code="500">Server error retrieving dashboard data (badge service failure).</response>
    [HttpGet("{id:int}/dashboard")]
    [ProducesResponseType(typeof(backend.DTOs.StudentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStudentDashboard(int id)
    {
        try
        {
            _logger.LogInformation("📊 GetStudentDashboard called for StudentId={StudentId}", id);
            
            // ── Authorization: Ensure user is reading their own dashboard or is an Admin ──
            var clerkUserId = User.FindFirst("sub")?.Value;
            if (clerkUserId == null || (!clerkUserId.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase) && !User.IsInRole("Admin")))
            {
                _logger.LogWarning("❌ Unauthorized access attempt: user {ClerkUserId} tried to access student dashboard for ID {StudentId}", clerkUserId, id);
                return Forbid();
            }
            
            // Fetch the user
            var user = await _userService.GetUserByIdAsync(id);
            if (user is null)
            {
                _logger.LogWarning("❌ Student not found: {StudentId}", id);
                return NotFound(new
                {
                    status = "error",
                    message = $"Student with ID {id} not found."
                });
            }

            _logger.LogInformation("✅ Student found: {StudentId}, XpTotal={XpTotal}", id, user.XpTotal);

            // Award any badges the user qualifies for but hasn't received yet
            await _badgeService.AwardUnlockedBadgesAsync(id);

            // Fetch earned badges with award dates
            var earnedBadgesWithDates = await _badgeService.GetEarnedBadgesWithAwardDateAsync(id);
            _logger.LogInformation("✅ Earned badges count: {Count}", earnedBadgesWithDates.Count());

            // Fetch all available badges
            var allBadges = await _badgeService.GetAllBadgesAsync();
            var allBadgesCount = allBadges.Count();
            _logger.LogInformation("✅ All badges count: {Count}", allBadgesCount);
            
            if (allBadgesCount == 0)
            {
                _logger.LogWarning("⚠️  WARNING: No badges found in database!");
            }

            // Create a set of earned badge IDs for quick lookup
            var earnedBadgeIds = new HashSet<int>(earnedBadgesWithDates.Select(b => b.Id));

            // Map earned badges with styling properties
            var earnedBadges = earnedBadgesWithDates
                .Select(b => new EarnedBadgeDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Description = b.Description,
                    XpThreshold = b.XpThreshold,
                    IconType = b.IconType,
                    IconColor = b.IconColor,
                    AwardDate = b.AwardDate
                })
                .ToList();
            
            _logger.LogInformation("✅ Mapped earned badges: {Count}", earnedBadges.Count);

            // Map all badges with earned status and styling properties
            var allBadgesList = allBadges
                .Select(b => new BadgeDashboardDto
                {
                    Id = b.BadgeId,
                    Name = b.BadgeName,
                    Description = b.BadgeDescription,
                    XpThreshold = b.XpThreshold,
                    IconType = b.IconType,
                    IconColor = b.IconColor,
                    UnlockHint = b.UnlockHint,
                    Earned = earnedBadgeIds.Contains(b.BadgeId)
                })
                .ToList();
            
            _logger.LogInformation("✅ Mapped all badges: {Count}", allBadgesList.Count);

            // Construct the dashboard DTO
            var dashboard = new StudentDashboardDto
            {
                StudentId = id,
                XpTotal = user.XpTotal,
                EarnedBadges = earnedBadges,
                AllBadges = allBadgesList
            };

            _logger.LogInformation("✅ Dashboard constructed: StudentId={StudentId}, EarnedBadges={EarnedCount}, AllBadges={AllCount}", 
                id, earnedBadges.Count, allBadgesList.Count);

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
    /// GET /api/students/{id}/progression — Retrieve the student's XP progression data for level display and progress bars.
    /// </summary>
    /// <remarks>
    /// Returns detailed XP progression information for rendering progress bars and level indicators:
    /// - UserId: The student identifier
    /// - XpTotal: Total accumulated experience points
    /// - CurrentLevel: Current player level (1-indexed)
    /// - XpPrevLevel: Cumulative XP threshold for current level (floor)
    /// - XpForNextLevel: Cumulative XP threshold for next level (ceiling)
    /// - XpInCurrentLevel: XP earned toward next level (current - floor)
    /// - XpNeededForLevel: Total XP required between levels (ceiling - floor)
    /// - ProgressPercentage: Percentage progress (0-100) within current level
    /// 
    /// Example: At 20 total XP with Level 1 = 0-99 XP:
    /// CurrentLevel: 1, XpInCurrentLevel: 20, XpNeededForLevel: 100, ProgressPercentage: 20.0
    /// </remarks>
    /// <param name="id">The student user ID (path parameter). Example: 238</param>
    /// <returns>
    /// Success response with UserProgressionDto containing userId (int), xpTotal (int), 
    /// currentLevel (int), xpInCurrentLevel (int), xpNeededForLevel (int), 
    /// progressPercentage (0-100 double).
    /// </returns>
    /// <response code="200">Progression data retrieved successfully.</response>
    /// <response code="404">Student not found with the given ID.</response>
    /// <response code="500">Server error calculating progression data.</response>
    [HttpGet("{id:int}/progression")]
    [ProducesResponseType(typeof(backend.DTOs.UserProgressionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetUserProgression(int id)
    {
        try
        {
            _logger.LogInformation("📈 GetUserProgression called for UserId={UserId}", id);
            
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

    // ── GET /api/students/{id}/attempts ───────────────────────────────

    /// <summary>
    /// GET /api/students/{id}/attempts — Retrieve paginated quiz attempt history for a student.
    /// </summary>
    /// <remarks>
    /// Returns paginated list of all quiz attempts with enriched metadata (quiz title, algorithm name).
    /// Results are ordered by completion date (most recent first).
    /// 
    /// Each attempt includes:
    /// - attemptId: Unique attempt identifier
    /// - quizId, quizTitle: Quiz identification and name
    /// - algorithmName: Algorithm being tested (derived from quiz)
    /// - score: Score percentage (0-100)
    /// - xpEarned: XP awarded for this attempt
    /// - passed: Boolean indicating if passed quiz.passScore threshold
    /// - completedAt, startedAt: Attempt timestamps (ISO 8601 UTC)
    /// 
    /// **Authorization**: Student may view own attempts; Admin may view any student.
    /// </remarks>
    /// <param name="id">The student user ID (path parameter). Example: 238</param>
    /// <param name="page">Page number (1-indexed). Defaults to 1. Example: 2</param>
    /// <param name="pageSize">Attempts per page, max 100. Defaults to 10. Example: 20</param>
    /// <returns>
    /// Success response with StudentAttemptHistoryResponseDto containing:
    /// attempts (array of {attemptId, quizId, quizTitle, algorithmName, score, xpEarned, passed, startedAt, completedAt}),
    /// page (int), pageSize (int), totalAttempts (int), totalPages (int), hasNextPage (bool), hasPreviousPage (bool).
    /// </returns>
    /// <response code="200">Attempt history retrieved and paginated successfully.</response>
    /// <response code="403">Unauthorized - student cannot view another student's attempts (Admin excepted).</response>
    /// <response code="404">Student not found with the given ID.</response>
    /// <response code="500">Server error retrieving attempt history (database or service failure).</response>
    [HttpGet("{id:int}/attempts")]
    [ProducesResponseType(typeof(backend.DTOs.StudentAttemptHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStudentAttemptHistory(
        int id, 
        [FromQuery(Name = "page")] int page = 1, 
        [FromQuery(Name = "pageSize")] int pageSize = 10)
    {
        try
        {
            _logger.LogInformation("📋 GetStudentAttemptHistory called for StudentId={StudentId}, Page={Page}, PageSize={PageSize}", id, page, pageSize);

            // ── Authorization: Ensure user is reading their own history or is an Admin ──
            var clerkUserId = User.FindFirst("sub")?.Value;
            if (clerkUserId == null || (!clerkUserId.Equals(id.ToString(), StringComparison.OrdinalIgnoreCase) && !User.IsInRole("Admin")))
            {
                _logger.LogWarning("❌ Unauthorized access attempt: user {ClerkUserId} tried to access attempt history for StudentId={StudentId}", clerkUserId, id);
                return Forbid();
            }

            // Verify student exists
            var user = await _userService.GetUserByIdAsync(id);
            if (user is null)
            {
                _logger.LogWarning("❌ Student not found: {StudentId}", id);
                return NotFound(new
                {
                    status = "error",
                    message = $"Student with ID {id} not found."
                });
            }

            _logger.LogInformation("✅ Student found: {StudentId}", id);

            // Get paginated attempt history with enriched data
            var attemptHistory = await _attemptService.GetUserAttemptHistoryAsync(id, page, pageSize);

            _logger.LogInformation("✅ Attempt history retrieved: Page={Page}, Attempts={Count}, TotalAttempts={Total}",
                page, attemptHistory.Attempts.Count(), attemptHistory.TotalAttempts);

            return Ok(new
            {
                status = "success",
                data = attemptHistory
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error retrieving attempt history for StudentId={StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving attempt history."
            });
        }
    }
}
