using System.Security.Claims;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Handles Clerk-authenticated user synchronisation with the local database.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserSyncController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserSyncController> _logger;

    public UserSyncController(IUserService userService, ILogger<UserSyncController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/users/sync
    /// Extracts user info from the Clerk JWT and upserts the user in the database.
    /// Returns 201 Created for new users or 200 OK for existing users.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> SyncUser()
    {
        try
        {
            // Extract claims from Clerk JWT
            var clerkUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? User.FindFirstValue("sub");

            var email = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.FindFirstValue("email");

            var username = User.FindFirstValue("name")
                           ?? User.FindFirstValue("preferred_username")
                           ?? User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(clerkUserId))
            {
                _logger.LogWarning("User sync failed: missing 'sub' claim in JWT.");
                return Unauthorized(new
                {
                    status = "error",
                    message = "Invalid token: missing user identifier."
                });
            }

            // Fall back to email prefix if username claim is absent
            username ??= email?.Split('@')[0] ?? "unknown";

            var (dto, isNewUser) = await _userService.SyncUserAsync(
                clerkUserId,
                email ?? string.Empty,
                username);

            if (isNewUser)
            {
                _logger.LogInformation("POST /api/users/sync — Created user {UserId}", dto.UserId);
                return StatusCode(StatusCodes.Status201Created, new
                {
                    status = "success",
                    message = "User created successfully.",
                    data = dto
                });
            }

            _logger.LogInformation("POST /api/users/sync — Existing user {UserId}", dto.UserId);
            return Ok(new
            {
                status = "success",
                message = "User already exists.",
                data = dto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in POST /api/users/sync");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = "error",
                message = "An unexpected error occurred while syncing the user.",
                detail = ex.Message
            });
        }
    }
}
