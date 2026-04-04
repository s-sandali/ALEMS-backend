using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

/// <summary>
/// Unit tests for <see cref="QuizService"/>.
///
/// Scenarios covered
/// -----------------
/// GetAllQuizzesAsync
///   1. Returns mapped DTOs for all quizzes
///   2. Returns empty list when no quizzes exist
/// GetActiveQuizzesAsync
///   3. Returns only active quizzes
/// GetQuizByIdAsync
///   4. Returns DTO when quiz found
///   5. Returns null when quiz not found
/// GetActiveQuizByIdAsync
///   6. Returns DTO when quiz is active
///   7. Returns null when quiz is inactive or not found
/// CreateQuizAsync
///   8. Throws KeyNotFoundException when user has no local account
///   9. Throws ArgumentException when algorithm does not exist
///  10. Returns DTO with correct fields when inputs are valid
///  11. Sets IsActive = true and resolves CreatedBy from user record
/// UpdateQuizAsync
///  12. Throws KeyNotFoundException when quiz not found
///  13. Applies all DTO fields and returns updated DTO
///  14. Updates description to null when DTO has null description
/// DeleteQuizAsync
///  15. Returns true on successful soft-delete
///  16. Returns false when quiz not found
/// </summary>
public class QuizServiceTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Quiz SampleQuiz(int id = 1, bool isActive = true) => new()
    {
        QuizId        = id,
        AlgorithmId   = 10,
        CreatedBy     = 5,
        Title         = "Sample Quiz",
        Description   = "A test quiz",
        TimeLimitMins = 30,
        PassScore     = 70,
        IsActive      = isActive,
        CreatedAt     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt     = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static User SampleUser(int id = 5) => new()
    {
        UserId      = id,
        ClerkUserId = "clerk_001",
        Email       = "admin@example.com",
        Username    = "admin",
        Role        = "Admin"
    };

    private static Algorithm SampleAlgorithm(int id = 10) => new()
    {
        AlgorithmId = id,
        Name        = "Bubble Sort"
    };

    private static QuizService BuildSut(
        Mock<IQuizRepository>      quizRepo,
        Mock<IUserRepository>?     userRepo = null,
        Mock<IAlgorithmRepository>? algoRepo = null) =>
        new(quizRepo.Object,
            (userRepo ?? new Mock<IUserRepository>()).Object,
            (algoRepo ?? new Mock<IAlgorithmRepository>()).Object,
            NullLogger<QuizService>.Instance);

    // -----------------------------------------------------------------------
    // GetAllQuizzesAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — GetAllQuizzesAsync: returns mapped DTOs for all quizzes")]
    public async Task Should_ReturnAllMappedDtos_When_GetAllQuizzesCalled()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetAllAsync())
                .ReturnsAsync(new[] { SampleQuiz(1), SampleQuiz(2, false) });

        // Act
        var result = (await BuildSut(quizRepo).GetAllQuizzesAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].QuizId.Should().Be(1);
        result[1].QuizId.Should().Be(2);
        result[1].IsActive.Should().BeFalse();
    }

    [Fact(DisplayName = "Scenario 2 — GetAllQuizzesAsync: returns empty list when no quizzes exist")]
    public async Task Should_ReturnEmpty_When_NoQuizzesExist()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Array.Empty<Quiz>());

        // Act
        var result = await BuildSut(quizRepo).GetAllQuizzesAsync();

        // Assert
        result.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // GetActiveQuizzesAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — GetActiveQuizzesAsync: delegates to GetActiveAsync and maps results")]
    public async Task Should_ReturnOnlyActiveQuizzes_When_GetActiveQuizzesCalled()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetActiveAsync())
                .ReturnsAsync(new[] { SampleQuiz(1, true) });

        // Act
        var result = (await BuildSut(quizRepo).GetActiveQuizzesAsync()).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].IsActive.Should().BeTrue();
        quizRepo.Verify(r => r.GetActiveAsync(), Times.Once);
    }

    // -----------------------------------------------------------------------
    // GetQuizByIdAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — GetQuizByIdAsync: returns DTO when quiz found")]
    public async Task Should_ReturnDto_When_QuizExists()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(SampleQuiz(1));

        // Act
        var result = await BuildSut(quizRepo).GetQuizByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.QuizId.Should().Be(1);
        result.Title.Should().Be("Sample Quiz");
        result.AlgorithmId.Should().Be(10);
        result.PassScore.Should().Be(70);
    }

    [Fact(DisplayName = "Scenario 5 — GetQuizByIdAsync: returns null when quiz not found")]
    public async Task Should_ReturnNull_When_QuizNotFound()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Quiz?)null);

        // Act
        var result = await BuildSut(quizRepo).GetQuizByIdAsync(99);

        // Assert
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetActiveQuizByIdAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 6 — GetActiveQuizByIdAsync: returns DTO when quiz is active")]
    public async Task Should_ReturnDto_When_QuizIsActive()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz(1, true));

        // Act
        var result = await BuildSut(quizRepo).GetActiveQuizByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
    }

    [Fact(DisplayName = "Scenario 7 — GetActiveQuizByIdAsync: returns null when quiz inactive or missing")]
    public async Task Should_ReturnNull_When_QuizInactiveOrMissing()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync((Quiz?)null);

        // Act
        var result = await BuildSut(quizRepo).GetActiveQuizByIdAsync(1);

        // Assert
        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // CreateQuizAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 8 — CreateQuizAsync: throws KeyNotFoundException when user has no local account")]
    public async Task Should_ThrowKeyNotFound_When_UserHasNoLocalAccount()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync((User?)null);

        var dto = new CreateQuizDto { AlgorithmId = 10, Title = "Test Quiz" };

        // Act & Assert
        await BuildSut(quizRepo, userRepo)
            .Invoking(s => s.CreateQuizAsync(dto, "clerk_001"))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*local account*");
    }

    [Fact(DisplayName = "Scenario 9 — CreateQuizAsync: throws ArgumentException when algorithm does not exist")]
    public async Task Should_ThrowArgumentException_When_AlgorithmNotFound()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        var userRepo = new Mock<IUserRepository>();
        var algoRepo = new Mock<IAlgorithmRepository>();

        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        algoRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Algorithm?)null);

        var dto = new CreateQuizDto { AlgorithmId = 999, Title = "Test Quiz" };

        // Act & Assert
        await BuildSut(quizRepo, userRepo, algoRepo)
            .Invoking(s => s.CreateQuizAsync(dto, "clerk_001"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*999*");
    }

    [Fact(DisplayName = "Scenario 10 — CreateQuizAsync: returns mapped DTO when inputs are valid")]
    public async Task Should_ReturnDto_When_CreateQuizInputsAreValid()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        var userRepo = new Mock<IUserRepository>();
        var algoRepo = new Mock<IAlgorithmRepository>();

        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        algoRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(SampleAlgorithm());
        quizRepo.Setup(r => r.CreateAsync(It.IsAny<Quiz>())).ReturnsAsync(SampleQuiz(42));

        var dto = new CreateQuizDto { AlgorithmId = 10, Title = "New Quiz", PassScore = 60 };

        // Act
        var result = await BuildSut(quizRepo, userRepo, algoRepo).CreateQuizAsync(dto, "clerk_001");

        // Assert
        result.Should().NotBeNull();
        result.QuizId.Should().Be(42);
        quizRepo.Verify(r => r.CreateAsync(It.IsAny<Quiz>()), Times.Once);
    }

    [Fact(DisplayName = "Scenario 11 — CreateQuizAsync: sets IsActive=true and resolves CreatedBy from user")]
    public async Task Should_SetIsActiveTrueAndResolveCreatedBy_When_Creating()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        var userRepo = new Mock<IUserRepository>();
        var algoRepo = new Mock<IAlgorithmRepository>();

        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser(id: 7));
        algoRepo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(SampleAlgorithm());

        Quiz? captured = null;
        quizRepo.Setup(r => r.CreateAsync(It.IsAny<Quiz>()))
                .Callback<Quiz>(q => captured = q)
                .ReturnsAsync(SampleQuiz(1));

        var dto = new CreateQuizDto { AlgorithmId = 10, Title = "New Quiz" };

        // Act
        await BuildSut(quizRepo, userRepo, algoRepo).CreateQuizAsync(dto, "clerk_001");

        // Assert
        captured.Should().NotBeNull();
        captured!.IsActive.Should().BeTrue();
        captured.CreatedBy.Should().Be(7);
        captured.AlgorithmId.Should().Be(10);
    }

    // -----------------------------------------------------------------------
    // UpdateQuizAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 12 — UpdateQuizAsync: throws KeyNotFoundException when quiz not found")]
    public async Task Should_ThrowKeyNotFound_When_UpdateTargetQuizMissing()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Quiz?)null);

        var dto = new UpdateQuizDto { Title = "Updated", IsActive = true };

        // Act & Assert
        await BuildSut(quizRepo)
            .Invoking(s => s.UpdateQuizAsync(99, dto))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact(DisplayName = "Scenario 13 — UpdateQuizAsync: applies all DTO fields and returns updated DTO")]
    public async Task Should_ApplyAllDtoFields_When_UpdatingExistingQuiz()
    {
        // Arrange
        var original = SampleQuiz(1);
        var updated  = SampleQuiz(1);
        updated.Title    = "Updated Title";
        updated.IsActive = false;
        updated.PassScore = 80;

        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.SetupSequence(r => r.GetByIdAsync(1))
                .ReturnsAsync(original)
                .ReturnsAsync(updated);
        quizRepo.Setup(r => r.UpdateAsync(It.IsAny<Quiz>())).ReturnsAsync(true);

        var dto = new UpdateQuizDto { Title = "Updated Title", IsActive = false, PassScore = 80 };

        // Act
        var result = await BuildSut(quizRepo).UpdateQuizAsync(1, dto);

        // Assert
        quizRepo.Verify(r => r.UpdateAsync(It.Is<Quiz>(q =>
            q.Title    == "Updated Title" &&
            q.IsActive == false           &&
            q.PassScore == 80)), Times.Once);
        result.Should().NotBeNull();
    }

    [Fact(DisplayName = "Scenario 14 — UpdateQuizAsync: propagates null description to entity")]
    public async Task Should_PropagateNullDescription_When_DtoDescriptionIsNull()
    {
        // Arrange
        var quiz = SampleQuiz(1);
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.SetupSequence(r => r.GetByIdAsync(1))
                .ReturnsAsync(quiz)
                .ReturnsAsync(quiz);
        quizRepo.Setup(r => r.UpdateAsync(It.IsAny<Quiz>())).ReturnsAsync(true);

        var dto = new UpdateQuizDto { Title = "T", Description = null, IsActive = true };

        // Act
        await BuildSut(quizRepo).UpdateQuizAsync(1, dto);

        // Assert
        quizRepo.Verify(r => r.UpdateAsync(
            It.Is<Quiz>(q => q.Description == null)), Times.Once);
    }

    // -----------------------------------------------------------------------
    // DeleteQuizAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 15 — DeleteQuizAsync: returns true on successful soft-delete")]
    public async Task Should_ReturnTrue_When_DeleteSucceeds()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        // Act
        var result = await BuildSut(quizRepo).DeleteQuizAsync(1);

        // Assert
        result.Should().BeTrue();
        quizRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }

    [Fact(DisplayName = "Scenario 16 — DeleteQuizAsync: returns false when quiz not found")]
    public async Task Should_ReturnFalse_When_DeleteTargetNotFound()
    {
        // Arrange
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.DeleteAsync(99)).ReturnsAsync(false);

        // Act
        var result = await BuildSut(quizRepo).DeleteQuizAsync(99);

        // Assert
        result.Should().BeFalse();
    }
}
