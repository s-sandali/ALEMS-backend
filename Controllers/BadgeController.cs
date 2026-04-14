using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// API endpoints for badge management and unlock tracking.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BadgeController : ControllerBase
{
    private readonly IBadgeService _badgeService;
    private readonly ILogger<BadgeController> _logger;

    public BadgeController(IBadgeService badgeService, ILogger<BadgeController> logger)
    {
        _badgeService = badgeService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all available badges sorted by XP threshold.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<BadgeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllBadges()
    {
        try
        {
            var badges = await _badgeService.GetAllBadgesAsync();
            return Ok(badges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all badges");
            return StatusCode(500, new { message = "An error occurred while retrieving badges" });
        }
    }

    /// <summary>
    /// Retrieves a specific badge by ID.
    /// </summary>
    [HttpGet("{badgeId}")]
    [ProducesResponseType(typeof(BadgeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBadgeById(int badgeId)
    {
        try
        {
            var badge = await _badgeService.GetBadgeByIdAsync(badgeId);
            if (badge == null)
                return NotFound(new { message = $"Badge with ID {badgeId} not found" });

            return Ok(badge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving badge {BadgeId}", badgeId);
            return StatusCode(500, new { message = "An error occurred while retrieving the badge" });
        }
    }

    /// <summary>
    /// Retrieves all badges earned by a specific user.
    /// </summary>
    [HttpGet("user/{userId}/earned")]
    [ProducesResponseType(typeof(IEnumerable<BadgeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEarnedBadges(int userId)
    {
        try
        {
            var badges = await _badgeService.GetUserEarnedBadgesAsync(userId);
            return Ok(badges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving earned badges for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user badges" });
        }
    }

    /// <summary>
    /// Retrieves all badges available (unlocked by XP) but not yet awarded to a user.
    /// </summary>
    [HttpGet("user/{userId}/available")]
    [ProducesResponseType(typeof(IEnumerable<BadgeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserAvailableBadges(int userId)
    {
        try
        {
            var badges = await _badgeService.GetUserAvailableBadgesAsync(userId);
            return Ok(badges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available badges for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving available badges" });
        }
    }

    /// <summary>
    /// Automatically awards any unlocked badges to a user based on their current XP.
    /// </summary>
    [HttpPost("user/{userId}/award-unlocked")]
    [ProducesResponseType(typeof(IEnumerable<BadgeResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AwardUnlockedBadges(int userId)
    {
        try
        {
            var awardedBadges = await _badgeService.AwardUnlockedBadgesAsync(userId);
            return Ok(new
            {
                message = "Badges awarded successfully",
                awardedBadges
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error awarding unlocked badges to user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while awarding badges" });
        }
    }

    /// <summary>
    /// Manually awards a specific badge to a user.
    /// </summary>
    [HttpPost("user/{userId}/award/{badgeId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AwardBadge(int userId, int badgeId)
    {
        try
        {
            var success = await _badgeService.AwardBadgeAsync(userId, badgeId);
            if (!success)
                return Conflict(new { message = "Badge already awarded to user or invalid user/badge" });

            return Ok(new { message = "Badge awarded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error awarding badge {BadgeId} to user {UserId}", badgeId, userId);
            return StatusCode(500, new { message = "An error occurred while awarding the badge" });
        }
    }
}
