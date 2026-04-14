using backend.DTOs;
using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Defines data-access operations for the Users table.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their Clerk user ID, or null if not found.
    /// </summary>
    Task<User?> GetByClerkUserIdAsync(string clerkUserId);

    /// <summary>
    /// Retrieves a user by email, or null if not found.
    /// </summary>
    Task<User?> GetByEmailAsync(string email);

    /// <summary>
    /// Inserts a new user and returns the created record with the generated ID.
    /// </summary>
    Task<User> CreateAsync(User user);
    /// <summary>
    /// Retrieves all users.
    /// </summary>
    Task<IEnumerable<User>> GetAllAsync();

    /// <summary>
    /// Retrieves a user by their auto-incrementing ID.
    /// </summary>
    Task<User?> GetByIdAsync(int id);

    /// <summary>
    /// Updates an existing user's role and is_active status.
    /// Returns true if successful, false if the user was not found.
    /// </summary>
    Task<bool> UpdateAsync(int id, string role, bool isActive);

    /// <summary>
    /// Soft deletes an existing user by setting is_active to false.
    /// Returns true if successful, false if the user was not found.
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Links a Clerk user ID to an existing user record (used after email-based fallback lookup).
    /// Returns true if the row was updated, false if the user was not found.
    /// </summary>
    Task<bool> LinkClerkUserIdAsync(int userId, string clerkUserId);

    /// <summary>
    /// Increments a user's accumulated XP.
    /// Returns true if the user was updated, false if not found.
    /// </summary>
    Task<bool> AddXpAsync(int userId, int xpEarned);

    /// <summary>
    /// Returns the top <paramref name="limit"/> active users ordered by XP descending.
    /// Each entry carries a 1-based rank derived from its position in the result set.
    /// </summary>
    Task<IEnumerable<LeaderboardEntryDto>> GetTopUsersAsync(int limit);

    /// <summary>
    /// Returns the 1-based rank of an active user by counting how many active users
    /// have strictly more XP. A user with the highest XP gets rank 1.
    /// Returns 1 when the user is not found (safe default).
    /// </summary>
    Task<int> GetUserRankAsync(int userId);
}
