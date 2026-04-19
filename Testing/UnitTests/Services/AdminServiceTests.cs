using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class AdminServiceTests
{
    private static AdminService BuildSut(
        Mock<IUserRepository> userRepository,
        Mock<IQuizRepository> quizRepository,
        Mock<IQuizAttemptRepository> attemptRepository)
        => new(
            userRepository.Object,
            quizRepository.Object,
            attemptRepository.Object,
            NullLogger<AdminService>.Instance);

    [Fact(DisplayName = "GetPlatformStatsAsync computes totals and pass rate")]
    public async Task GetPlatformStatsAsync_WithAttempts_ComputesExpectedStats()
    {
        var userRepository = new Mock<IUserRepository>();
        var quizRepository = new Mock<IQuizRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();

        userRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new User { UserId = 1, Username = "alice", Email = "a@example.com", XpTotal = 120 },
                new User { UserId = 2, Username = "bob", Email = "b@example.com", XpTotal = 90 },
                new User { UserId = 3, Username = "cara", Email = "c@example.com", XpTotal = 60 }
            });

        quizRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new Quiz { QuizId = 1, Title = "Q1" },
                new Quiz { QuizId = 2, Title = "Q2" }
            });

        attemptRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new QuizAttempt { AttemptId = 1, UserId = 1, QuizId = 1, Score = 80, Passed = true },
                new QuizAttempt { AttemptId = 2, UserId = 2, QuizId = 2, Score = 70, Passed = false },
                new QuizAttempt { AttemptId = 3, UserId = 3, QuizId = 1, Score = 90, Passed = true }
            });

        var result = await BuildSut(userRepository, quizRepository, attemptRepository)
            .GetPlatformStatsAsync();

        result.TotalUsers.Should().Be(3);
        result.TotalQuizzes.Should().Be(2);
        result.TotalAttempts.Should().Be(3);
        result.AveragePassRate.Should().BeApproximately(66.6667, 0.0001);
    }

    [Fact(DisplayName = "GetPlatformStatsAsync returns zero pass rate when no attempts exist")]
    public async Task GetPlatformStatsAsync_NoAttempts_ReturnsZeroPassRate()
    {
        var userRepository = new Mock<IUserRepository>();
        var quizRepository = new Mock<IQuizRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();

        userRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<User>());
        quizRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Quiz>());
        attemptRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<QuizAttempt>());

        var result = await BuildSut(userRepository, quizRepository, attemptRepository)
            .GetPlatformStatsAsync();

        result.TotalUsers.Should().Be(0);
        result.TotalQuizzes.Should().Be(0);
        result.TotalAttempts.Should().Be(0);
        result.AveragePassRate.Should().Be(0);
    }

    [Fact(DisplayName = "GetLeaderboardAsync sorts by XP and assigns tie-aware ranks")]
    public async Task GetLeaderboardAsync_AssignsRanksAndAggregatesAttemptData()
    {
        var userRepository = new Mock<IUserRepository>();
        var quizRepository = new Mock<IQuizRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();

        userRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new User { UserId = 1, Username = "alice", Email = "a@example.com", XpTotal = 300 },
                new User { UserId = 2, Username = "bob", Email = "b@example.com", XpTotal = 300 },
                new User { UserId = 3, Username = "cara", Email = "c@example.com", XpTotal = 120 }
            });

        attemptRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new QuizAttempt { AttemptId = 10, UserId = 1, QuizId = 1, Score = 80, Passed = true },
                new QuizAttempt { AttemptId = 11, UserId = 1, QuizId = 2, Score = 60, Passed = true },
                new QuizAttempt { AttemptId = 12, UserId = 3, QuizId = 3, Score = 90, Passed = true }
            });

        var result = (await BuildSut(userRepository, quizRepository, attemptRepository)
            .GetLeaderboardAsync()).ToList();

        result.Should().HaveCount(3);

        result[0].UserId.Should().Be(1);
        result[0].Rank.Should().Be(1);
        result[0].AttemptCount.Should().Be(2);
        result[0].AverageScore.Should().BeApproximately(70.0, 0.0001);

        result[1].UserId.Should().Be(2);
        result[1].Rank.Should().Be(1);
        result[1].AttemptCount.Should().Be(0);
        result[1].AverageScore.Should().Be(0);

        result[2].UserId.Should().Be(3);
        result[2].Rank.Should().Be(3);
        result[2].AttemptCount.Should().Be(1);
        result[2].AverageScore.Should().BeApproximately(90.0, 0.0001);

        userRepository.Verify(r => r.GetAllAsync(), Times.Once);
        attemptRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }
}