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
    private readonly ILogger<StudentController> _logger;

    public StudentController(
        IUserService userService,
        IBadgeService badgeService,
        ILogger<StudentController> logger)
    {
        _userService = userService;
        _badgeService = badgeService;
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
            // Fetch the user
            var user = await _userService.GetUserByIdAsync(id);
            if (user is null)
            {
                return NotFound(new
                {
                    status = "error",
                    message = $"Student with ID {id} not found."
                });
            }

            // Fetch earned badges with award dates
            var earnedBadgesWithDates = await _badgeService.GetEarnedBadgesWithAwardDateAsync(id);

            // Fetch all available badges
            var allBadges = await _badgeService.GetAllBadgesAsync();

            // Create a set of earned badge IDs for quick lookup
            var earnedBadgeIds = new HashSet<int>(earnedBadgesWithDates.Select(b => b.Id));

            // Map earned badges with icons
            var earnedBadges = earnedBadgesWithDates
                .Select(b => new EarnedBadgeDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    Icon = GetBadgeIcon(b.Id, b.Name),
                    AwardDate = b.AwardDate
                });

            // Map all badges with earned status and icons
            var allBadgesList = allBadges
                .Select(b => new BadgeDashboardDto
                {
                    Id = b.BadgeId,
                    Name = b.BadgeName,
                    Icon = GetBadgeIcon(b.BadgeId, b.BadgeName),
                    Earned = earnedBadgeIds.Contains(b.BadgeId)
                });

            // Construct the dashboard DTO
            var dashboard = new StudentDashboardDto
            {
                StudentId = id,
                XpTotal = user.XpTotal,
                EarnedBadges = earnedBadges,
                AllBadges = allBadgesList
            };

            return Ok(new
            {
                status = "success",
                data = dashboard
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving student dashboard for ID {StudentId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while retrieving the dashboard."
            });
        }
    }

    /// <summary>
    /// Maps badge IDs/names to emoji icons for display.
    /// Add more mappings as needed based on badge names.
    /// </summary>
    private static string GetBadgeIcon(int badgeId, string badgeName)
    {
        return badgeName.ToLower() switch
        {
            var s when s.Contains("first") => "🎯",
            var s when s.Contains("master") || s.Contains("expert") => "🏆",
            var s when s.Contains("power") => "⚡",
            var s when s.Contains("legend") => "👑",
            var s when s.Contains("streak") => "🔥",
            var s when s.Contains("speed") => "🚀",
            var s when s.Contains("challenge") => "💪",
            var s when s.Contains("algorithm") => "🧮",
            var s when s.Contains("quiz") => "📚",
            var s when s.Contains("solve") || s.Contains("solun") => "✅",
            _ => "⭐"  // Default icon
        };
    }
}
