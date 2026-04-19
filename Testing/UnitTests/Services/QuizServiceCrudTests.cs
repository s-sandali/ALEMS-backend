using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class QuizServiceCrudTests
{
    private static Quiz CreateQuiz(int quizId = 3) => new()
    {
        QuizId = quizId,
        AlgorithmId = 7,
        CreatedBy = 10,
        Title = "Heap Sort Quiz",
        Description = "desc",
        TimeLimitMins = 15,
        PassScore = 70,
        IsActive = true,
        CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static QuizService BuildSut(
        Mock<IQuizRepository> quizRepository,
        Mock<IUserRepository>? userRepository = null,
        Mock<IAlgorithmRepository>? algorithmRepository = null,
        Mock<IQuizAttemptRepository>? attemptRepository = null)
    {
        return new QuizService(
            quizRepository.Object,
            (userRepository ?? new Mock<IUserRepository>()).Object,
            (algorithmRepository ?? new Mock<IAlgorithmRepository>()).Object,
            (attemptRepository ?? new Mock<IQuizAttemptRepository>()).Object,
            NullLogger<QuizService>.Instance);
    }

    [Fact(DisplayName = "GetActiveQuizzesAsync maps active quizzes to DTOs")]
    public async Task GetActiveQuizzesAsync_ReturnsMappedDtos()
    {
        var quizRepository = new Mock<IQuizRepository>();
        quizRepository
            .Setup(r => r.GetActiveAsync())
            .ReturnsAsync(new[] { CreateQuiz(1), CreateQuiz(2) });

        var result = (await BuildSut(quizRepository).GetActiveQuizzesAsync()).ToList();

        result.Should().HaveCount(2);
        result.Select(q => q.QuizId).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "GetActiveQuizByIdAsync returns null when the quiz is inactive or missing")]
    public async Task GetActiveQuizByIdAsync_Missing_ReturnsNull()
    {
        var quizRepository = new Mock<IQuizRepository>();
        quizRepository
            .Setup(r => r.GetActiveByIdAsync(99))
            .ReturnsAsync((Quiz?)null);

        var result = await BuildSut(quizRepository).GetActiveQuizByIdAsync(99);

        result.Should().BeNull();
    }

    [Fact(DisplayName = "CreateQuizAsync throws when the creator has not been synced locally")]
    public async Task CreateQuizAsync_CreatorMissing_ThrowsKeyNotFoundException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var userRepository = new Mock<IUserRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync((User?)null);

        var dto = new CreateQuizDto { AlgorithmId = 7, Title = "New Quiz" };

        var action = () => BuildSut(quizRepository, userRepository, algorithmRepository)
            .CreateQuizAsync(dto, "clerk_001");

        await action.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("*local account*");
    }

    [Fact(DisplayName = "CreateQuizAsync throws when the referenced algorithm does not exist")]
    public async Task CreateQuizAsync_AlgorithmMissing_ThrowsArgumentException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var userRepository = new Mock<IUserRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync(new User { UserId = 10, ClerkUserId = "clerk_001" });
        algorithmRepository
            .Setup(r => r.GetByIdAsync(7))
            .ReturnsAsync((Algorithm?)null);

        var dto = new CreateQuizDto { AlgorithmId = 7, Title = "New Quiz" };

        var action = () => BuildSut(quizRepository, userRepository, algorithmRepository)
            .CreateQuizAsync(dto, "clerk_001");

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Algorithm with ID 7 does not exist.");
    }

    [Fact(DisplayName = "CreateQuizAsync persists a new active quiz when inputs are valid")]
    public async Task CreateQuizAsync_ValidInput_CreatesQuiz()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var userRepository = new Mock<IUserRepository>();
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync(new User { UserId = 10, ClerkUserId = "clerk_001" });
        algorithmRepository
            .Setup(r => r.GetByIdAsync(7))
            .ReturnsAsync(new Algorithm { AlgorithmId = 7, Name = "Heap Sort" });
        quizRepository
            .Setup(r => r.CreateAsync(It.IsAny<Quiz>()))
            .ReturnsAsync((Quiz quiz) =>
            {
                quiz.QuizId = 22;
                quiz.CreatedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
                quiz.UpdatedAt = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
                return quiz;
            });

        var dto = new CreateQuizDto
        {
            AlgorithmId = 7,
            Title = "New Quiz",
            Description = "Basics",
            TimeLimitMins = 15,
            PassScore = 80
        };

        var result = await BuildSut(quizRepository, userRepository, algorithmRepository)
            .CreateQuizAsync(dto, "clerk_001");

        result.QuizId.Should().Be(22);
        result.CreatedBy.Should().Be(10);
        result.IsActive.Should().BeTrue();
        result.Title.Should().Be("New Quiz");
    }

    [Fact(DisplayName = "UpdateQuizAsync throws when the quiz cannot be found")]
    public async Task UpdateQuizAsync_QuizMissing_ThrowsKeyNotFoundException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        quizRepository
            .Setup(r => r.GetByIdAsync(99))
            .ReturnsAsync((Quiz?)null);

        var action = () => BuildSut(quizRepository).UpdateQuizAsync(99, new UpdateQuizDto { Title = "Updated", IsActive = true });

        await action.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("Quiz with ID 99 was not found.");
    }

    [Fact(DisplayName = "UpdateQuizAsync updates quiz fields and returns the refreshed entity")]
    public async Task UpdateQuizAsync_QuizExists_ReturnsUpdatedDto()
    {
        var existing = CreateQuiz(5);
        var updated = CreateQuiz(5);
        updated.Title = "Updated Quiz";
        updated.PassScore = 85;
        updated.IsActive = false;

        var quizRepository = new Mock<IQuizRepository>();
        quizRepository
            .SetupSequence(r => r.GetByIdAsync(5))
            .ReturnsAsync(existing)
            .ReturnsAsync(updated);
        quizRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Quiz>()))
            .ReturnsAsync(true);

        var result = await BuildSut(quizRepository).UpdateQuizAsync(5, new UpdateQuizDto
        {
            Title = "Updated Quiz",
            Description = "Updated desc",
            TimeLimitMins = 20,
            PassScore = 85,
            IsActive = false
        });

        result.Title.Should().Be("Updated Quiz");
        result.PassScore.Should().Be(85);
        result.IsActive.Should().BeFalse();
    }

    [Fact(DisplayName = "DeleteQuizAsync returns the repository result for soft deletion")]
    public async Task DeleteQuizAsync_ReturnsRepositoryResult()
    {
        var quizRepository = new Mock<IQuizRepository>();
        quizRepository
            .Setup(r => r.DeleteAsync(7))
            .ReturnsAsync(true);

        var result = await BuildSut(quizRepository).DeleteQuizAsync(7);

        result.Should().BeTrue();
    }
}
