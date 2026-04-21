using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class BadgeServiceTests
{
    private static Badge CreateBadge(int id, string name, int xpThreshold = 0, int? algorithmId = null) => new()
    {
        BadgeId = id,
        BadgeName = name,
        BadgeDescription = $"{name} description",
        XpThreshold = xpThreshold,
        IconType = "star",
        IconColor = "#123456",
        UnlockHint = "Keep going",
        AlgorithmId = algorithmId,
        CreatedAt = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc)
    };

    private static BadgeService BuildSut(Mock<IBadgeRepository> badgeRepository, Mock<IUserRepository>? userRepository = null)
    {
        return new BadgeService(badgeRepository.Object, (userRepository ?? new Mock<IUserRepository>()).Object);
    }

    [Fact(DisplayName = "GetAllBadgesAsync maps badge entities to response DTOs")]
    public async Task GetAllBadgesAsync_ReturnsMappedDtos()
    {
        var badgeRepository = new Mock<IBadgeRepository>();
        badgeRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                CreateBadge(1, "Bronze", xpThreshold: 100),
                CreateBadge(2, "Binary Search", algorithmId: 7)
            });

        var result = (await BuildSut(badgeRepository).GetAllBadgesAsync()).ToList();

        result.Should().HaveCount(2);
        result[0].BadgeId.Should().Be(1);
        result[0].BadgeName.Should().Be("Bronze");
        result[0].XpThreshold.Should().Be(100);
        result[1].BadgeId.Should().Be(2);
        result[1].BadgeName.Should().Be("Binary Search");
    }

    [Fact(DisplayName = "AwardUnlockedBadgesAsync returns empty when the user does not exist")]
    public async Task AwardUnlockedBadgesAsync_UserMissing_ReturnsEmpty()
    {
        var badgeRepository = new Mock<IBadgeRepository>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(42))
            .ReturnsAsync((User?)null);

        var result = await BuildSut(badgeRepository, userRepository).AwardUnlockedBadgesAsync(42);

        result.Should().BeEmpty();
        badgeRepository.Verify(r => r.GetUnlockedBadgesByUserIdAsync(It.IsAny<int>()), Times.Never);
        badgeRepository.Verify(r => r.GetUnlockedAlgorithmBadgesByUserIdAsync(It.IsAny<int>()), Times.Never);
        badgeRepository.Verify(r => r.AwardBadgeToUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "AwardUnlockedBadgesAsync awards unlocked XP and algorithm badges and skips duplicates")]
    public async Task AwardUnlockedBadgesAsync_UserExists_AwardsOnlySuccessfulBadges()
    {
        const int userId = 8;

        var xpBadge = CreateBadge(1, "Bronze", xpThreshold: 100);
        var duplicateXpBadge = CreateBadge(2, "Silver", xpThreshold: 200);
        var algorithmBadge = CreateBadge(3, "Heap Master", algorithmId: 4);

        var badgeRepository = new Mock<IBadgeRepository>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(new User { UserId = userId, Username = "alice" });

        badgeRepository
            .Setup(r => r.GetUnlockedBadgesByUserIdAsync(userId))
            .ReturnsAsync(new[] { xpBadge, duplicateXpBadge });

        badgeRepository
            .Setup(r => r.GetUnlockedAlgorithmBadgesByUserIdAsync(userId))
            .ReturnsAsync(new[] { algorithmBadge });

        badgeRepository
            .Setup(r => r.GetByIdAsync(xpBadge.BadgeId))
            .ReturnsAsync(xpBadge);
        badgeRepository
            .Setup(r => r.GetByIdAsync(duplicateXpBadge.BadgeId))
            .ReturnsAsync(duplicateXpBadge);
        badgeRepository
            .Setup(r => r.GetByIdAsync(algorithmBadge.BadgeId))
            .ReturnsAsync(algorithmBadge);

        badgeRepository
            .Setup(r => r.AwardBadgeToUserAsync(userId, xpBadge.BadgeId))
            .ReturnsAsync(true);
        badgeRepository
            .Setup(r => r.AwardBadgeToUserAsync(userId, duplicateXpBadge.BadgeId))
            .ReturnsAsync(false);
        badgeRepository
            .Setup(r => r.AwardBadgeToUserAsync(userId, algorithmBadge.BadgeId))
            .ReturnsAsync(true);

        var result = (await BuildSut(badgeRepository, userRepository).AwardUnlockedBadgesAsync(userId)).ToList();

        result.Should().HaveCount(2);
        result.Select(b => b.BadgeId).Should().Equal(xpBadge.BadgeId, algorithmBadge.BadgeId);
        badgeRepository.Verify(r => r.AwardBadgeToUserAsync(userId, duplicateXpBadge.BadgeId), Times.Once);
    }

    [Fact(DisplayName = "AwardBadgeAsync returns false when the user does not exist")]
    public async Task AwardBadgeAsync_UserMissing_ReturnsFalse()
    {
        var badgeRepository = new Mock<IBadgeRepository>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync((User?)null);

        var result = await BuildSut(badgeRepository, userRepository).AwardBadgeAsync(10, 4);

        result.Should().BeFalse();
        badgeRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        badgeRepository.Verify(r => r.AwardBadgeToUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "AwardBadgeAsync returns false when the badge does not exist")]
    public async Task AwardBadgeAsync_BadgeMissing_ReturnsFalse()
    {
        var badgeRepository = new Mock<IBadgeRepository>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(new User { UserId = 10 });
        badgeRepository
            .Setup(r => r.GetByIdAsync(4))
            .ReturnsAsync((Badge?)null);

        var result = await BuildSut(badgeRepository, userRepository).AwardBadgeAsync(10, 4);

        result.Should().BeFalse();
        badgeRepository.Verify(r => r.AwardBadgeToUserAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "AwardBadgeAsync delegates to the repository when user and badge exist")]
    public async Task AwardBadgeAsync_ValidUserAndBadge_ReturnsRepositoryResult()
    {
        var badge = CreateBadge(4, "Binary Search");
        var badgeRepository = new Mock<IBadgeRepository>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(new User { UserId = 10 });
        badgeRepository
            .Setup(r => r.GetByIdAsync(4))
            .ReturnsAsync(badge);
        badgeRepository
            .Setup(r => r.AwardBadgeToUserAsync(10, 4))
            .ReturnsAsync(true);

        var result = await BuildSut(badgeRepository, userRepository).AwardBadgeAsync(10, 4);

        result.Should().BeTrue();
        badgeRepository.Verify(r => r.AwardBadgeToUserAsync(10, 4), Times.Once);
    }

    [Fact(DisplayName = "GetEarnedBadgesWithAwardDateAsync maps award metadata for dashboard consumption")]
    public async Task GetEarnedBadgesWithAwardDateAsync_ReturnsMappedDtos()
    {
        var awardedAt = new DateTime(2026, 4, 18, 8, 30, 0, DateTimeKind.Utc);
        var badgeRepository = new Mock<IBadgeRepository>();

        badgeRepository
            .Setup(r => r.GetEarnedBadgesWithAwardDateAsync(3))
            .ReturnsAsync(new List<(Badge Badge, DateTime AwardedAt)>
            {
                (CreateBadge(5, "Bronze", xpThreshold: 100), awardedAt)
            });

        var result = (await BuildSut(badgeRepository).GetEarnedBadgesWithAwardDateAsync(3)).Single();

        result.Should().BeEquivalentTo(new EarnedBadgeDto
        {
            Id = 5,
            Name = "Bronze",
            Description = "Bronze description",
            XpThreshold = 100,
            IconType = "star",
            IconColor = "#123456",
            AwardDate = awardedAt
        });
    }
}
