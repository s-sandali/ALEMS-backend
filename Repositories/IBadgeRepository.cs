using backend.Models;

namespace backend.Repositories;

/// <summary>
/// Defines data-access operations for the badges table.
/// </summary>
public interface IBadgeRepository
{
    /// <summary>
    /// Retrieves all badges sorted by XP threshold.
    /// </summary>
    Task<IEnumerable<Badge>> GetAllAsync();

    /// <summary>
    /// Retrieves a badge by its ID.
    /// </summary>
    Task<Badge?> GetByIdAsync(int badgeId);

    /// <summary>
    /// Retrieves badges that a user has earned based on their XP total.
    /// </summary>
    Task<IEnumerable<Badge>> GetEarnedBadgesByUserIdAsync(int userId);

    /// <summary>
    /// Retrieves badges that a user has NOT yet earned based on their XP total.
    /// </summary>
    Task<IEnumerable<Badge>> GetUnlockedBadgesByUserIdAsync(int userId);

    /// <summary>
    /// Inserts a new badge and returns the created record with the generated ID.
    /// </summary>
    Task<Badge> CreateAsync(Badge badge);

    /// <summary>
    /// Updates an existing badge.
    /// Returns true if successful, false if the badge was not found.
    /// </summary>
    Task<bool> UpdateAsync(int badgeId, Badge badge);

    /// <summary>
    /// Deletes a badge.
    /// Returns true if successful, false if the badge was not found.
    /// </summary>
    Task<bool> DeleteAsync(int badgeId);

    /// <summary>
    /// Awards a badge to a user (inserts into user_badges table).
    /// Returns true if successful, false if user/badge doesn't exist or already awarded.
    /// </summary>
    Task<bool> AwardBadgeToUserAsync(int userId, int badgeId);

    /// <summary>
    /// Retrieves algorithm badges the user qualifies for (passed a quiz for the algorithm)
    /// but has not yet been awarded.
    /// </summary>
    Task<IEnumerable<Badge>> GetUnlockedAlgorithmBadgesByUserIdAsync(int userId);

    /// <summary>
    /// Retrieves earned badges for a user with their award dates.
    /// Returns joined UserBadge and Badge data sorted by XP threshold ascending.
    /// </summary>
    Task<IEnumerable<(Badge Badge, DateTime AwardedAt)>> GetEarnedBadgesWithAwardDateAsync(int userId);
}
