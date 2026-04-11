using backend.DTOs;
using backend.Models;
using backend.Repositories;

namespace backend.Services;

/// <summary>
/// Business logic implementation for badge management.
/// </summary>
public class BadgeService : IBadgeService
{
    private readonly IBadgeRepository _badgeRepository;
    private readonly IUserRepository _userRepository;

    public BadgeService(IBadgeRepository badgeRepository, IUserRepository userRepository)
    {
        _badgeRepository = badgeRepository;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BadgeResponseDto>> GetAllBadgesAsync()
    {
        var badges = await _badgeRepository.GetAllAsync();
        return badges.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<BadgeResponseDto?> GetBadgeByIdAsync(int badgeId)
    {
        var badge = await _badgeRepository.GetByIdAsync(badgeId);
        return badge == null ? null : MapToDto(badge);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BadgeResponseDto>> GetUserEarnedBadgesAsync(int userId)
    {
        var badges = await _badgeRepository.GetEarnedBadgesByUserIdAsync(userId);
        return badges.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BadgeResponseDto>> GetUserAvailableBadgesAsync(int userId)
    {
        var badges = await _badgeRepository.GetUnlockedBadgesByUserIdAsync(userId);
        return badges.Select(MapToDto);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BadgeResponseDto>> AwardUnlockedBadgesAsync(int userId)
    {
        // Get the user's current XP
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return Enumerable.Empty<BadgeResponseDto>();

        // Get all badges that user hasn't earned yet but XP qualifies for
        var unlockedBadges = await _badgeRepository.GetUnlockedBadgesByUserIdAsync(userId);
        var awards = new List<BadgeResponseDto>();

        foreach (var badge in unlockedBadges)
        {
            // Award the badge
            var success = await AwardBadgeAsync(userId, badge.BadgeId);
            if (success)
            {
                awards.Add(MapToDto(badge));
            }
        }

        return awards;
    }

    /// <inheritdoc />
    public async Task<bool> AwardBadgeAsync(int userId, int badgeId)
    {
        // Verify user exists
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return false;

        // Verify badge exists
        var badge = await _badgeRepository.GetByIdAsync(badgeId);
        if (badge == null)
            return false;

        // Award the badge
        return await _badgeRepository.AwardBadgeToUserAsync(userId, badgeId);
    }

    /// <inheritdoc />
    public async Task<BadgeResponseDto> CreateBadgeAsync(Badge badge)
    {
        var created = await _badgeRepository.CreateAsync(badge);
        return MapToDto(created);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EarnedBadgeDto>> GetEarnedBadgesWithAwardDateAsync(int userId)
    {
        var earnedBadges = await _badgeRepository.GetEarnedBadgesWithAwardDateAsync(userId);
        return earnedBadges.Select(item => new EarnedBadgeDto
        {
            Id = item.Badge.BadgeId,  // Field name: Id (instead of BadgeId)
            Name = item.Badge.BadgeName,  // Field name: Name (instead of BadgeName)
            Description = item.Badge.BadgeDescription,  // BadgeDescription
            XpThreshold = item.Badge.XpThreshold,  // XP required
            IconType = item.Badge.IconType,  // lucide-react icon type
            IconColor = item.Badge.IconColor,  // Icon color in hex
            AwardDate = item.AwardedAt  // Field name: AwardDate (instead of AwardedAt)
        });
    }

    /// <summary>
    /// Maps a Badge domain model to a BadgeResponseDto.
    /// </summary>
    private static BadgeResponseDto MapToDto(Badge badge)
    {
        return new BadgeResponseDto
        {
            BadgeId = badge.BadgeId,
            BadgeName = badge.BadgeName,
            BadgeDescription = badge.BadgeDescription,
            XpThreshold = badge.XpThreshold,
            IconType = badge.IconType,
            IconColor = badge.IconColor,
            UnlockHint = badge.UnlockHint
        };
    }
}
