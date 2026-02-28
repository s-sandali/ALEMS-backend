using backend.DTOs;

namespace backend.Services;

/// <summary>
/// Defines business-logic operations for user synchronization.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Synchronises a Clerk-authenticated user with the local database.
    /// Returns the DTO and a flag indicating whether a new record was created.
    /// </summary>
    Task<(UserResponseDto Dto, bool IsNewUser)> SyncUserAsync(
        string clerkUserId, string email, string username);

    /// <summary>
    /// Creates a new user (admin operation). Returns null if email already exists.
    /// </summary>
    Task<UserResponseDto?> CreateUserAsync(string email, string username, string role);
    /// <summary>
    /// Retrieves all users.
    /// </summary>
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();

    /// <summary>
    /// Retrieves a user by ID. Returns null if not found.
    /// </summary>
    Task<UserResponseDto?> GetUserByIdAsync(int id);

    /// <summary>
    /// Updates a user's role and active status.
    /// Returns the updated DTO, or null if the user was not found.
    /// </summary>
    Task<UserResponseDto?> UpdateUserAsync(int id, string role, bool isActive);
}
