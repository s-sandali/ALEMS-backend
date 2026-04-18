using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class QuizAttemptServiceAttemptHistoryTests
{
    private static QuizAttemptService BuildSut(
        Mock<IQuizAttemptRepository> attemptRepository,
        Mock<IQuizRepository> quizRepository,
        Mock<IAlgorithmRepository> algorithmRepository)
    {
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var badgeService = new Mock<IBadgeService>();

        return new QuizAttemptService(
            quizRepository.Object,
            questionRepository.Object,
            userRepository.Object,
            attemptRepository.Object,
            algorithmRepository.Object,
            badgeService.Object,
            NullLogger<QuizAttemptService>.Instance);
    }

    [Fact(DisplayName = "GetUserAttemptHistoryAsync normalizes invalid pagination and returns empty page")]
    public async Task GetUserAttemptHistoryAsync_InvalidPaging_ReturnsNormalizedEmptyResponse()
    {
        const int userId = 15;

        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var quizRepository = new Mock<IQuizRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        attemptRepository
            .Setup(r => r.GetAttemptsForUserAsync(userId, 1, 10))
            .ReturnsAsync((Attempts: Enumerable.Empty<QuizAttempt>(), TotalCount: 0));

        var result = await BuildSut(attemptRepository, quizRepository, algorithmRepository)
            .GetUserAttemptHistoryAsync(userId, pageNumber: 0, pageSize: 0);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalAttempts.Should().Be(0);
        result.Attempts.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeFalse();

        quizRepository.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
        algorithmRepository.Verify(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
    }

    [Fact(DisplayName = "GetUserAttemptHistoryAsync caps page size at 100 and enriches attempts")]
    public async Task GetUserAttemptHistoryAsync_PageSizeAboveCap_UsesCapAndEnrichesData()
    {
        const int userId = 8;
        var now = new DateTime(2026, 4, 19, 10, 0, 0, DateTimeKind.Utc);

        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var quizRepository = new Mock<IQuizRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        var attempts = new List<QuizAttempt>
        {
            new()
            {
                AttemptId = 101,
                UserId = userId,
                QuizId = 5,
                Score = 78,
                XpEarned = 40,
                Passed = true,
                StartedAt = now.AddMinutes(-20),
                CompletedAt = now
            }
        };

        attemptRepository
            .Setup(r => r.GetAttemptsForUserAsync(userId, 2, 100))
            .ReturnsAsync((Attempts: attempts, TotalCount: 150));

        quizRepository
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 5 }))))
            .ReturnsAsync(new[]
            {
                new Quiz { QuizId = 5, AlgorithmId = 9, Title = "Binary Search" }
            });

        algorithmRepository
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 9 }))))
            .ReturnsAsync(new[]
            {
                new Algorithm { AlgorithmId = 9, Name = "Searching" }
            });

        var result = await BuildSut(attemptRepository, quizRepository, algorithmRepository)
            .GetUserAttemptHistoryAsync(userId, pageNumber: 2, pageSize: 500);

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(100);
        result.TotalAttempts.Should().Be(150);
        result.TotalPages.Should().Be(2);
        result.HasNextPage.Should().BeFalse();
        result.HasPreviousPage.Should().BeTrue();

        var item = result.Attempts.Single();
        item.AttemptId.Should().Be(101);
        item.QuizTitle.Should().Be("Binary Search");
        item.AlgorithmName.Should().Be("Searching");
    }

    [Fact(DisplayName = "GetUserAttemptHistoryAsync falls back to unknown labels when lookup data is missing")]
    public async Task GetUserAttemptHistoryAsync_MissingLookupData_UsesUnknownFallbacks()
    {
        const int userId = 21;
        var now = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var quizRepository = new Mock<IQuizRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        var attempts = new List<QuizAttempt>
        {
            new()
            {
                AttemptId = 201,
                UserId = userId,
                QuizId = 10,
                Score = 90,
                XpEarned = 50,
                Passed = true,
                StartedAt = now.AddMinutes(-15),
                CompletedAt = now.AddMinutes(-10)
            },
            new()
            {
                AttemptId = 202,
                UserId = userId,
                QuizId = 999,
                Score = 40,
                XpEarned = 0,
                Passed = false,
                StartedAt = now.AddMinutes(-30),
                CompletedAt = now.AddMinutes(-25)
            }
        };

        attemptRepository
            .Setup(r => r.GetAttemptsForUserAsync(userId, 1, 20))
            .ReturnsAsync((Attempts: attempts, TotalCount: 2));

        quizRepository
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.OrderBy(i => i).SequenceEqual(new[] { 10, 999 }))))
            .ReturnsAsync(new[]
            {
                new Quiz { QuizId = 10, AlgorithmId = 7, Title = "Quick Sort Quiz" }
            });

        algorithmRepository
            .Setup(r => r.GetByIdsAsync(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 }))))
            .ReturnsAsync(new[]
            {
                new Algorithm { AlgorithmId = 7, Name = "Quick Sort" }
            });

        var result = await BuildSut(attemptRepository, quizRepository, algorithmRepository)
            .GetUserAttemptHistoryAsync(userId, pageNumber: 1, pageSize: 20);

        var items = result.Attempts.ToList();
        items.Select(x => x.AttemptId).Should().Equal(201, 202);

        items[0].QuizTitle.Should().Be("Quick Sort Quiz");
        items[0].AlgorithmName.Should().Be("Quick Sort");

        items[1].QuizTitle.Should().Be("Unknown Quiz");
        items[1].AlgorithmName.Should().Be("Unknown Algorithm");
    }
}