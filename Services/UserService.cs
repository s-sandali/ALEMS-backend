using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Handles user synchronisation logic: look up or create a user record.
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(UserResponseDto Dto, bool IsNewUser)> SyncUserAsync(
        string clerkUserId, string email, string username)
    {
        // Check if the user already exists
        var existingUser = await _userRepository.GetByClerkUserIdAsync(clerkUserId);

        if (existingUser is not null)
        {
            _logger.LogInformation("User sync: existing user found — ClerkId={ClerkId}", clerkUserId);
            return (MapToDto(existingUser), false);
        }

        // Create a new user with defaults
        var newUser = new User
        {
            ClerkUserId = clerkUserId,
            Email = email,
            Username = username,
            Role = "Student",
            XpTotal = 0,
            IsActive = true
        };

        var createdUser = await _userRepository.CreateAsync(newUser);
        _logger.LogInformation("User sync: new user created — ClerkId={ClerkId}, UserId={UserId}",
            clerkUserId, createdUser.UserId);

        return (MapToDto(createdUser), true);
    }

    /// <summary>
    /// Maps a <see cref="User"/> domain model to a <see cref="UserResponseDto"/>.
    /// </summary>
    private static UserResponseDto MapToDto(User user)
    {
        return new UserResponseDto
        {
            UserId = user.UserId,
            ClerkUserId = user.ClerkUserId,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role,
            XpTotal = user.XpTotal,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
