using System.Security.Claims;
using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Synchronises a Clerk-authenticated user with the local database using JWT claims.
/// </summary>
public class UserSyncService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserSyncService> _logger;

    public UserSyncService(IUserRepository userRepository, ILogger<UserSyncService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Reads the <c>sub</c>, email and username claims from <paramref name="principal"/>,
    /// then either returns the existing user record or creates a new one.
    /// </summary>
    public async Task<(UserResponseDto Dto, bool IsNewUser)> SyncUserFromClaimsAsync(
        ClaimsPrincipal principal)
    {
        // Extract Clerk user ID from the 'sub' claim — mandatory
        var clerkUserId = principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(clerkUserId))
            throw new ArgumentException("JWT is missing the required 'sub' claim.", nameof(principal));

        var email    = principal.FindFirstValue(ClaimTypes.Email)     ?? string.Empty;
        var username = principal.FindFirstValue("username")            ?? email;

        // Return existing user without touching the DB again
        var existing = await _userRepository.GetByClerkUserIdAsync(clerkUserId);
        if (existing is not null)
        {
            _logger.LogInformation(
                "UserSyncService: existing user found — ClerkId={ClerkId}", clerkUserId);
            return (MapToDto(existing), false);
        }

        // First-time login — provision a new record
        var newUser = new User
        {
            ClerkUserId = clerkUserId,
            Email       = email,
            Username    = username,
            Role        = "Student",
            XpTotal     = 0,
            IsActive    = true
        };

        var created = await _userRepository.CreateAsync(newUser);

        _logger.LogInformation(
            "UserSyncService: new user created — ClerkId={ClerkId}, UserId={UserId}",
            clerkUserId, created.UserId);

        return (MapToDto(created), true);
    }

    // -------------------------------------------------------------------------

    private static UserResponseDto MapToDto(User user) => new()
    {
        UserId      = user.UserId,
        ClerkUserId = user.ClerkUserId,
        Email       = user.Email,
        Username    = user.Username,
        Role        = user.Role,
        XpTotal     = user.XpTotal,
        IsActive    = user.IsActive,
        CreatedAt   = user.CreatedAt,
        UpdatedAt   = user.UpdatedAt
    };
}
