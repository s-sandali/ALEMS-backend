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
}
