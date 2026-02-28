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
}
