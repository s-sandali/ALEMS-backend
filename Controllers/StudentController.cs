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
    private readonly ILogger<StudentController> _logger;

    public StudentController(
        IUserService userService,
        IBadgeService badgeService,
        ILevelingService levelingService,
        ILogger<StudentController> logger)
    {
        _userService = userService;
        _badgeService = badgeService;
        _levelingService = levelingService;
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
}
