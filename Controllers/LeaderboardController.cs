using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Leaderboard endpoint — returns the top users by XP with the current user always included.
/// </summary>
[ApiController]
[Route("api/leaderboard")]
[Authorize]
[Produces("application/json")]
public class LeaderboardController : ControllerBase
{
    private readonly ILeaderboardService _leaderboardService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<LeaderboardController> _logger;

    public LeaderboardController(
        ILeaderboardService leaderboardService,
        IUserRepository userRepository,
        ILogger<LeaderboardController> logger)
    {
        _leaderboardService = leaderboardService;
        _userRepository     = userRepository;
        _logger             = logger;
    }

    /// <summary>
    /// GET /api/leaderboard — Returns the top 10 users by XP.
    /// The authenticated user is always included; if they are outside the top 10 they
    /// are appended as the final entry with their actual rank.
    /// </summary>
    /// <response code="200">Leaderboard returned successfully.</response>
    /// <response code="401">No valid Clerk JWT provided.</response>
    /// <response code="404">Authenticated user account not found in the database.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLeaderboard()
    {
        try
        {
            var clerkUserId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(clerkUserId))
            {
                return Unauthorized(new
                {
                    status  = "error",
                    message = "Unable to identify authenticated user."
                });
            }

            var user = await _userRepository.GetByClerkUserIdAsync(clerkUserId);
            if (user is null)
            {
                _logger.LogWarning("Leaderboard request from unknown Clerk user: {ClerkUserId}", clerkUserId);
                return NotFound(new
                {
                    status  = "error",
                    message = "User account not found."
                });
            }

            var leaderboard = await _leaderboardService.GetLeaderboardAsync(user.UserId);

            _logger.LogInformation(
                "Leaderboard served: {EntryCount} entries, currentUserId={UserId}",
                leaderboard.Count(), user.UserId);

            return Ok(new
            {
                status = "success",
                data   = leaderboard
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving leaderboard");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status  = "error",
                message = "An unexpected error occurred while retrieving the leaderboard."
            });
        }
    }
}
