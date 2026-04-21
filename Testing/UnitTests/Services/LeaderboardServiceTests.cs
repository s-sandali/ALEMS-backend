using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class LeaderboardServiceTests
{
    [Fact(DisplayName = "GetLeaderboardAsync marks the current user when they are already in the top list")]
    public async Task GetLeaderboardAsync_CurrentUserAlreadyInTop_MarksEntry()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetTopUsersAsync(10))
            .ReturnsAsync(new List<LeaderboardEntryDto>
            {
                new() { UserId = 1, Username = "alice", Rank = 1, XpTotal = 400 },
                new() { UserId = 2, Username = "bob", Rank = 2, XpTotal = 350 }
            });
        userRepository
            .Setup(r => r.GetUserRankAsync(2))
            .ReturnsAsync(2);

        var result = (await new LeaderboardService(userRepository.Object).GetLeaderboardAsync(2)).ToList();

        result.Should().HaveCount(2);
        result.Single(entry => entry.UserId == 2).IsCurrentUser.Should().BeTrue();
        userRepository.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "GetLeaderboardAsync appends the current user when they are outside the top list")]
    public async Task GetLeaderboardAsync_CurrentUserOutsideTop_AppendsProfile()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetTopUsersAsync(10))
            .ReturnsAsync(new List<LeaderboardEntryDto>
            {
                new() { UserId = 1, Username = "alice", Rank = 1, XpTotal = 400 }
            });
        userRepository
            .Setup(r => r.GetUserRankAsync(7))
            .ReturnsAsync(17);
        userRepository
            .Setup(r => r.GetByIdAsync(7))
            .ReturnsAsync(new User { UserId = 7, Username = "zoe", XpTotal = 99 });

        var result = (await new LeaderboardService(userRepository.Object).GetLeaderboardAsync(7)).ToList();

        result.Should().HaveCount(2);
        result.Last().Should().BeEquivalentTo(new LeaderboardEntryDto
        {
            UserId = 7,
            Username = "zoe",
            XpTotal = 99,
            Rank = 17,
            IsCurrentUser = true
        });
    }

    [Fact(DisplayName = "GetLeaderboardAsync leaves the list unchanged when the current user profile is unavailable")]
    public async Task GetLeaderboardAsync_CurrentUserMissing_DoesNotAppend()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetTopUsersAsync(10))
            .ReturnsAsync(new List<LeaderboardEntryDto>
            {
                new() { UserId = 1, Username = "alice", Rank = 1, XpTotal = 400 }
            });
        userRepository
            .Setup(r => r.GetUserRankAsync(7))
            .ReturnsAsync(17);
        userRepository
            .Setup(r => r.GetByIdAsync(7))
            .ReturnsAsync((User?)null);

        var result = (await new LeaderboardService(userRepository.Object).GetLeaderboardAsync(7)).ToList();

        result.Should().HaveCount(1);
    }
}
