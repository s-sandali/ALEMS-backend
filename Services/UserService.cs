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

    /// <inheritdoc />
    public async Task<UserResponseDto?> CreateUserAsync(string email, string username, string role)
    {
        // Check for duplicate email
        var existing = await _userRepository.GetByEmailAsync(email);
        if (existing is not null)
        {
            _logger.LogWarning("Admin create user: duplicate email — {Email}", email);
            return null; // signals duplicate to the controller
        }

        var newUser = new User
        {
            ClerkUserId = string.Empty,  // NULL in DB — admin-created, no Clerk link
            Email = email,
            Username = username,
            Role = role,
            XpTotal = 0,
            IsActive = true
        };

        var created = await _userRepository.CreateAsync(newUser);
        _logger.LogInformation("Admin create user: UserId={UserId}, Email={Email}", created.UserId, email);

        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<UserResponseDto?> GetUserByIdAsync(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        return user is not null ? MapToDto(user) : null;
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
