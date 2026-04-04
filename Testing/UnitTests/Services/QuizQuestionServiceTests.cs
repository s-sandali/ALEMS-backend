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
/// Unit tests for <see cref="QuizQuestionService"/>.
///
/// Scenarios covered
/// -----------------
/// GetByQuizIdAsync (admin)
///   1. Throws KeyNotFoundException when quiz does not exist
///   2. Returns ordered admin DTOs (CorrectOption included) when quiz exists
/// GetActiveQuestionsForStudentAsync
///   3. Throws KeyNotFoundException when quiz inactive or not found
///   4. Returns student DTOs with CorrectOption and Explanation omitted
/// GetByIdAsync
///   5. Returns null when question not found
///   6. Returns DTO when question found
/// CreateAsync
///   7. Throws KeyNotFoundException when quiz does not exist
///   8. Calls IXpService to compute XP for the difficulty tier
///   9. Returns DTO with correct fields after creation
/// UpdateAsync
///  10. Throws KeyNotFoundException when question not found
///  11. Recalculates XP when difficulty changes
///  12. Returns refreshed DTO after update
/// DeleteAsync
///  13. Returns false when question not found
///  14. Returns true on successful soft-delete
/// </summary>
public class QuizQuestionServiceTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Quiz SampleQuiz(int id = 1, bool active = true) => new()
    {
        QuizId    = id,
        Title     = "Test Quiz",
        IsActive  = active,
        PassScore = 70
    };

    private static QuizQuestion SampleQuestion(int id = 1, int quizId = 1) => new()
    {
        QuestionId    = id,
        QuizId        = quizId,
        QuestionType  = "MCQ",
        QuestionText  = "What is O(n)?",
        OptionA       = "Constant",
        OptionB       = "Linear",
        OptionC       = "Quadratic",
        OptionD       = "Logarithmic",
        CorrectOption = "B",
        Difficulty    = "easy",
        XpReward      = 10,
        Explanation   = "Linear time grows with input.",
        OrderIndex    = 0,
        IsActive      = true,
        CreatedAt     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static QuizQuestionService BuildSut(
        Mock<IQuizQuestionRepository> qRepo,
        Mock<IQuizRepository>?        quizRepo = null,
        Mock<IXpService>?             xpService = null) =>
        new(qRepo.Object,
            (quizRepo  ?? new Mock<IQuizRepository>()).Object,
            (xpService ?? new Mock<IXpService>()).Object,
            NullLogger<QuizQuestionService>.Instance);

    // -----------------------------------------------------------------------
    // GetByQuizIdAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — GetByQuizIdAsync: throws KeyNotFoundException when quiz not found")]
    public async Task Should_ThrowKeyNotFound_When_QuizNotFoundForAdmin()
    {
        // Arrange
        var qRepo    = new Mock<IQuizQuestionRepository>();
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Quiz?)null);

        // Act & Assert
        await BuildSut(qRepo, quizRepo)
            .Invoking(s => s.GetByQuizIdAsync(99))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact(DisplayName = "Scenario 2 — GetByQuizIdAsync: returns admin DTOs including CorrectOption")]
    public async Task Should_ReturnAdminDtosWithCorrectOption_When_QuizExists()
    {
        // Arrange
        var qRepo    = new Mock<IQuizQuestionRepository>();
        var quizRepo = new Mock<IQuizRepository>();

        quizRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(SampleQuiz());
        qRepo.Setup(r => r.GetByQuizIdAsync(1))
             .ReturnsAsync(new[] { SampleQuestion(1), SampleQuestion(2) });

        // Act
        var result = (await BuildSut(qRepo, quizRepo).GetByQuizIdAsync(1)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].CorrectOption.Should().Be("B");
        result[0].Explanation.Should().NotBeNullOrEmpty();
        result[0].QuizId.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // GetActiveQuestionsForStudentAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — GetActiveQuestionsForStudentAsync: throws KeyNotFoundException when quiz inactive")]
    public async Task Should_ThrowKeyNotFound_When_QuizInactiveForStudent()
    {
        // Arrange
        var qRepo    = new Mock<IQuizQuestionRepository>();
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync((Quiz?)null);

        // Act & Assert
        await BuildSut(qRepo, quizRepo)
            .Invoking(s => s.GetActiveQuestionsForStudentAsync(1))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "Scenario 4 — GetActiveQuestionsForStudentAsync: omits CorrectOption and Explanation")]
    public async Task Should_OmitCorrectOptionAndExplanation_When_ReturningStudentDtos()
    {
        // Arrange
        var qRepo    = new Mock<IQuizQuestionRepository>();
        var quizRepo = new Mock<IQuizRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz(1, true));
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(new[] { SampleQuestion() });

        // Act
        var result = (await BuildSut(qRepo, quizRepo).GetActiveQuestionsForStudentAsync(1)).ToList();

        // Assert
        result.Should().HaveCount(1);
        // StudentQuizQuestionResponseDto has no CorrectOption or Explanation properties
        var dto = result[0];
        dto.QuestionId.Should().Be(1);
        dto.OptionA.Should().Be("Constant");
        dto.Difficulty.Should().Be("easy");
        // The DTO type itself guarantees absence of CorrectOption/Explanation
        dto.GetType().GetProperty("CorrectOption").Should().BeNull();
        dto.GetType().GetProperty("Explanation").Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetByIdAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 5 — GetByIdAsync: returns null when question not found")]
    public async Task Should_ReturnNull_When_QuestionNotFound()
    {
        // Arrange
        var qRepo = new Mock<IQuizQuestionRepository>();
        qRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((QuizQuestion?)null);

        // Act
        var result = await BuildSut(qRepo).GetByIdAsync(99);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName = "Scenario 6 — GetByIdAsync: returns mapped DTO when question found")]
    public async Task Should_ReturnMappedDto_When_QuestionFound()
    {
        // Arrange
        var qRepo = new Mock<IQuizQuestionRepository>();
        qRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(SampleQuestion());

        // Act
        var result = await BuildSut(qRepo).GetByIdAsync(1);

        // Assert
        result.Should().NotBeNull();
        result!.QuestionId.Should().Be(1);
        result.QuestionText.Should().Be("What is O(n)?");
        result.CorrectOption.Should().Be("B");
    }

    // -----------------------------------------------------------------------
    // CreateAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 7 — CreateAsync: throws KeyNotFoundException when quiz not found")]
    public async Task Should_ThrowKeyNotFound_When_QuizNotFoundOnCreate()
    {
        // Arrange
        var qRepo    = new Mock<IQuizQuestionRepository>();
        var quizRepo = new Mock<IQuizRepository>();
        quizRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((Quiz?)null);

        var dto = new CreateQuizQuestionDto
        {
            QuestionType  = "MCQ",
            QuestionText  = "Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "A",
            Difficulty    = "easy"
        };

        // Act & Assert
        await BuildSut(qRepo, quizRepo)
            .Invoking(s => s.CreateAsync(99, dto))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact(DisplayName = "Scenario 8 — CreateAsync: delegates XP calculation to IXpService")]
    public async Task Should_DelegateXpCalculation_When_CreatingQuestion()
    {
        // Arrange
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var quizRepo  = new Mock<IQuizRepository>();
        var xpService = new Mock<IXpService>();

        quizRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(SampleQuiz());
        xpService.Setup(x => x.CalculateXP("quiz", "medium")).Returns(20);
        qRepo.Setup(r => r.CreateAsync(It.IsAny<QuizQuestion>()))
             .ReturnsAsync((QuizQuestion q) => { q.QuestionId = 10; return q; });

        var dto = new CreateQuizQuestionDto
        {
            QuestionType  = "MCQ",
            QuestionText  = "What is O(n log n)?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "B",
            Difficulty    = "medium"
        };

        // Act
        await BuildSut(qRepo, quizRepo, xpService).CreateAsync(1, dto);

        // Assert
        xpService.Verify(x => x.CalculateXP("quiz", "medium"), Times.Once);
        qRepo.Verify(r => r.CreateAsync(
            It.Is<QuizQuestion>(q => q.XpReward == 20 && q.Difficulty == "medium")), Times.Once);
    }

    [Fact(DisplayName = "Scenario 9 — CreateAsync: returns DTO with all fields mapped after creation")]
    public async Task Should_ReturnMappedDto_When_QuestionCreatedSuccessfully()
    {
        // Arrange
        var qRepo    = new Mock<IQuizQuestionRepository>();
        var quizRepo = new Mock<IQuizRepository>();
        var xpSvc    = new Mock<IXpService>();

        quizRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(SampleQuiz());
        xpSvc.Setup(x => x.CalculateXP(It.IsAny<string>(), It.IsAny<string>())).Returns(10);
        qRepo.Setup(r => r.CreateAsync(It.IsAny<QuizQuestion>()))
             .ReturnsAsync(SampleQuestion(id: 7, quizId: 1));

        var dto = new CreateQuizQuestionDto
        {
            QuestionType  = "MCQ",
            QuestionText  = "Question text",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "B",
            Difficulty    = "easy",
            OrderIndex    = 2
        };

        // Act
        var result = await BuildSut(qRepo, quizRepo, xpSvc).CreateAsync(1, dto);

        // Assert
        result.Should().NotBeNull();
        result.QuestionId.Should().Be(7);
        result.QuizId.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    // UpdateAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 10 — UpdateAsync: throws KeyNotFoundException when question not found")]
    public async Task Should_ThrowKeyNotFound_When_QuestionNotFoundOnUpdate()
    {
        // Arrange
        var qRepo = new Mock<IQuizQuestionRepository>();
        qRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((QuizQuestion?)null);

        var dto = new UpdateQuizQuestionDto
        {
            QuestionType = "MCQ", QuestionText = "Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "A", Difficulty = "easy", IsActive = true
        };

        // Act & Assert
        await BuildSut(qRepo)
            .Invoking(s => s.UpdateAsync(99, dto))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*");
    }

    [Fact(DisplayName = "Scenario 11 — UpdateAsync: recalculates XP when difficulty changes")]
    public async Task Should_RecalculateXp_When_DifficultyChangesOnUpdate()
    {
        // Arrange
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var xpService = new Mock<IXpService>();

        qRepo.SetupSequence(r => r.GetByIdAsync(1))
             .ReturnsAsync(SampleQuestion())   // first call: fetch existing
             .ReturnsAsync(SampleQuestion());  // second call: return updated

        xpService.Setup(x => x.CalculateXP("quiz", "hard")).Returns(30);
        qRepo.Setup(r => r.UpdateAsync(It.IsAny<QuizQuestion>())).ReturnsAsync(true);

        var dto = new UpdateQuizQuestionDto
        {
            QuestionType  = "MCQ",
            QuestionText  = "Updated Q?",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "C",
            Difficulty    = "hard",
            IsActive      = true
        };

        // Act
        await BuildSut(qRepo, null, xpService).UpdateAsync(1, dto);

        // Assert
        xpService.Verify(x => x.CalculateXP("quiz", "hard"), Times.Once);
        qRepo.Verify(r => r.UpdateAsync(
            It.Is<QuizQuestion>(q => q.XpReward == 30 && q.Difficulty == "hard")), Times.Once);
    }

    [Fact(DisplayName = "Scenario 12 — UpdateAsync: fetches fresh row after update and returns mapped DTO")]
    public async Task Should_ReturnRefreshedDto_When_UpdateSucceeds()
    {
        // Arrange
        var original = SampleQuestion();
        var refreshed = SampleQuestion();
        refreshed.QuestionText = "Refreshed Q";

        var qRepo    = new Mock<IQuizQuestionRepository>();
        var xpSvc    = new Mock<IXpService>();

        qRepo.SetupSequence(r => r.GetByIdAsync(1))
             .ReturnsAsync(original)
             .ReturnsAsync(refreshed);
        qRepo.Setup(r => r.UpdateAsync(It.IsAny<QuizQuestion>())).ReturnsAsync(true);
        xpSvc.Setup(x => x.CalculateXP(It.IsAny<string>(), It.IsAny<string>())).Returns(10);

        var dto = new UpdateQuizQuestionDto
        {
            QuestionType = "MCQ", QuestionText = "Refreshed Q",
            OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
            CorrectOption = "B", Difficulty = "easy", IsActive = true
        };

        // Act
        var result = await BuildSut(qRepo, null, xpSvc).UpdateAsync(1, dto);

        // Assert
        result.QuestionText.Should().Be("Refreshed Q");
    }

    // -----------------------------------------------------------------------
    // DeleteAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 13 — DeleteAsync: returns false when question not found")]
    public async Task Should_ReturnFalse_When_DeleteTargetNotFound()
    {
        // Arrange
        var qRepo = new Mock<IQuizQuestionRepository>();
        qRepo.Setup(r => r.DeleteAsync(99)).ReturnsAsync(false);

        // Act
        var result = await BuildSut(qRepo).DeleteAsync(99);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName = "Scenario 14 — DeleteAsync: returns true on successful soft-delete")]
    public async Task Should_ReturnTrue_When_DeleteSucceeds()
    {
        // Arrange
        var qRepo = new Mock<IQuizQuestionRepository>();
        qRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        // Act
        var result = await BuildSut(qRepo).DeleteAsync(1);

        // Assert
        result.Should().BeTrue();
        qRepo.Verify(r => r.DeleteAsync(1), Times.Once);
    }
}
