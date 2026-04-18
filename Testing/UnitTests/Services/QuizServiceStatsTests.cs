using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class QuizServiceStatsTests
{
    private static QuizService BuildSut(
        Mock<IQuizRepository> quizRepository,
        Mock<IQuizAttemptRepository> attemptRepository)
    {
        var userRepository = new Mock<IUserRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        return new QuizService(
            quizRepository.Object,
            userRepository.Object,
            algorithmRepository.Object,
            attemptRepository.Object,
            NullLogger<QuizService>.Instance);
    }

    [Fact(DisplayName = "GetStatsAsync returns null when quiz does not exist")]
    public async Task GetStatsAsync_QuizMissing_ReturnsNull()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();

        quizRepository
            .Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Quiz?)null);

        var result = await BuildSut(quizRepository, attemptRepository).GetStatsAsync(999);

        result.Should().BeNull();
        attemptRepository.Verify(r => r.GetAllAsync(), Times.Never);
    }

    [Fact(DisplayName = "GetStatsAsync returns zeroed stats when quiz has no attempts")]
    public async Task GetStatsAsync_NoAttempts_ReturnsZeroStats()
    {
        const int quizId = 5;

        var quizRepository = new Mock<IQuizRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();

        quizRepository
            .Setup(r => r.GetByIdAsync(quizId))
            .ReturnsAsync(new Quiz { QuizId = quizId, Title = "Graphs Intro" });

        attemptRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(Array.Empty<QuizAttempt>());

        var result = await BuildSut(quizRepository, attemptRepository).GetStatsAsync(quizId);

        result.Should().NotBeNull();
        result!.AttemptCount.Should().Be(0);
        result.AverageScore.Should().Be(0);
        result.PassRate.Should().Be(0);
    }

    [Fact(DisplayName = "GetStatsAsync computes attempt count, average score, and pass rate for a quiz")]
    public async Task GetStatsAsync_WithAttempts_ComputesFilteredStats()
    {
        const int quizId = 7;

        var quizRepository = new Mock<IQuizRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();

        quizRepository
            .Setup(r => r.GetByIdAsync(quizId))
            .ReturnsAsync(new Quiz { QuizId = quizId, Title = "Quick Sort" });

        attemptRepository
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[]
            {
                new QuizAttempt { AttemptId = 1, QuizId = quizId, UserId = 1, Score = 80, Passed = true },
                new QuizAttempt { AttemptId = 2, QuizId = quizId, UserId = 2, Score = 60, Passed = false },
                new QuizAttempt { AttemptId = 3, QuizId = 999, UserId = 3, Score = 100, Passed = true }
            });

        var result = await BuildSut(quizRepository, attemptRepository).GetStatsAsync(quizId);

        result.Should().NotBeNull();
        result!.AttemptCount.Should().Be(2);
        result.AverageScore.Should().BeApproximately(70.0, 0.0001);
        result.PassRate.Should().BeApproximately(50.0, 0.0001);
    }
}