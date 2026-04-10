using backend.DTOs;
using backend.Models;

namespace backend.Services;

/// <summary>
/// Defines business logic operations for badge awarding and retrieval.
/// </summary>
public interface IBadgeService
{
    /// <summary>
    /// Retrieves all available badges.
    /// </summary>
    Task<IEnumerable<BadgeResponseDto>> GetAllBadgesAsync();

    /// <summary>
    /// Retrieves a badge by its ID.
    /// </summary>
    Task<BadgeResponseDto?> GetBadgeByIdAsync(int badgeId);

    /// <summary>
    /// Retrieves all badges earned by a user.
    /// </summary>
    Task<IEnumerable<BadgeResponseDto>> GetUserEarnedBadgesAsync(int userId);

    /// <summary>
    /// Retrieves all badges that are unlocked but not yet awarded to a user.
    /// </summary>
    Task<IEnumerable<BadgeResponseDto>> GetUserAvailableBadgesAsync(int userId);

    /// <summary>
    /// Automatically awards any unlocked badges to a user based on their XP.
    /// Returns the list of newly awarded badges.
    /// </summary>
    Task<IEnumerable<BadgeResponseDto>> AwardUnlockedBadgesAsync(int userId);

    /// <summary>
    /// Manually awards a badge to a user.
    /// Returns true if successful, false if badge already awarded or user/badge doesn't exist.
    /// </summary>
    Task<bool> AwardBadgeAsync(int userId, int badgeId);

    /// <summary>
    /// Creates a new badge.
    /// </summary>
    Task<BadgeResponseDto> CreateBadgeAsync(Badge badge);
}
