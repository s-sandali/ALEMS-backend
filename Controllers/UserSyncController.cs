using System.Security.Claims;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

/// <summary>
/// Handles Clerk-authenticated user synchronisation with the local database.
/// </summary>
/// <remarks>
/// Requires a valid Clerk JWT (any authenticated role).
/// </remarks>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UserSyncController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IClerkService _clerkService;
    private readonly ILogger<UserSyncController> _logger;

    public UserSyncController(
        IUserService userService,
        IClerkService clerkService,
        ILogger<UserSyncController> logger)
    {
        _userService = userService;
        _clerkService = clerkService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/users/sync — Upsert the authenticated user from their Clerk JWT.
    /// </summary>
    /// <remarks>
    /// Extracts <c>sub</c>, <c>email</c>, and <c>name</c> / <c>preferred_username</c>
    /// claims from the Bearer token and creates or returns the matching local record.
    /// </remarks>
    /// <response code="200">Existing user record returned.</response>
    /// <response code="201">New user created and returned.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
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

            // "role_missing" is injected by the JWT middleware when public_metadata.role
            // is absent. Use this marker (not the role value itself) to decide whether
            // to write the default role to Clerk — the role value is always "User" after
            // the middleware fallback, so checking it directly would never trigger the PATCH.
            var roleMissing = User.HasClaim("role_missing", "true");

            var (dto, isNewUser) = await _userService.SyncUserAsync(
                clerkUserId,
                email ?? string.Empty,
                username);

            // If public_metadata.role was absent AND the DB role is the default "User",
            // write it to Clerk so future tokens carry the claim automatically.
            // Guard against overwriting an elevated role (e.g. Admin) that was set in
            // Clerk/DB manually but isn't yet present in the JWT (e.g. no JWT template).
            if (roleMissing && string.Equals(dto.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                await _clerkService.SetUserRoleAsync(clerkUserId, "User");
                _logger.LogInformation(
                    "POST /api/users/sync — Default role 'User' written to Clerk for ClerkId={ClerkId}",
                    clerkUserId);
            }
            else if (roleMissing)
            {
                // Role is missing from JWT but DB has a non-default role — sync it back to Clerk.
                await _clerkService.SetUserRoleAsync(clerkUserId, dto.Role);
                _logger.LogInformation(
                    "POST /api/users/sync — Restored role '{Role}' to Clerk for ClerkId={ClerkId}",
                    dto.Role, clerkUserId);
            }

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
