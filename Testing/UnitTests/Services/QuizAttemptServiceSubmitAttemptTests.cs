using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class QuizAttemptServiceSubmitAttemptTests
{
    private static Quiz CreateQuiz(int quizId = 5, int passScore = 70) => new()
    {
        QuizId = quizId,
        AlgorithmId = 3,
        CreatedBy = 99,
        Title = "Binary Search Basics",
        PassScore = passScore,
        IsActive = true,
        CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static QuizQuestion CreateQuestion(int questionId, string correctOption, int xpReward, string explanation = "Because") => new()
    {
        QuestionId = questionId,
        QuizId = 5,
        QuestionText = $"Question {questionId}",
        OptionA = "A",
        OptionB = "B",
        OptionC = "C",
        OptionD = "D",
        CorrectOption = correctOption,
        Difficulty = "easy",
        XpReward = xpReward,
        Explanation = explanation,
        OrderIndex = questionId,
        CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static QuizAttemptService BuildSut(
        Mock<IQuizRepository> quizRepository,
        Mock<IQuizQuestionRepository> questionRepository,
        Mock<IUserRepository> userRepository,
        Mock<IQuizAttemptRepository> attemptRepository,
        Mock<IBadgeService> badgeService)
    {
        var algorithmRepository = new Mock<IAlgorithmRepository>();

        return new QuizAttemptService(
            quizRepository.Object,
            questionRepository.Object,
            userRepository.Object,
            attemptRepository.Object,
            algorithmRepository.Object,
            badgeService.Object,
            NullLogger<QuizAttemptService>.Instance);
    }

    [Fact(DisplayName = "SubmitAttemptAsync throws when the active quiz cannot be found")]
    public async Task SubmitAttemptAsync_QuizMissing_ThrowsKeyNotFoundException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        quizRepository
            .Setup(r => r.GetActiveByIdAsync(5))
            .ReturnsAsync((Quiz?)null);

        var action = () => BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(5, "clerk_001", new CreateQuizAttemptDto());

        await action.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("Quiz with ID 5 does not exist.");

        userRepository.Verify(r => r.GetByClerkUserIdAsync(It.IsAny<string>()), Times.Never);
        attemptRepository.Verify(r => r.SubmitAttemptTransactionalAsync(It.IsAny<QuizAttempt>(), It.IsAny<IEnumerable<AttemptAnswer>>(), It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "SubmitAttemptAsync throws when the authenticated user is not synced locally")]
    public async Task SubmitAttemptAsync_UserMissing_ThrowsKeyNotFoundException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        quizRepository
            .Setup(r => r.GetActiveByIdAsync(5))
            .ReturnsAsync(CreateQuiz());
        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync((User?)null);

        var action = () => BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(5, "clerk_001", new CreateQuizAttemptDto());

        await action.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("*local account*");

        questionRepository.Verify(r => r.GetByQuizIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "SubmitAttemptAsync throws when a quiz has no active questions")]
    public async Task SubmitAttemptAsync_NoQuestions_ThrowsArgumentException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        quizRepository.Setup(r => r.GetActiveByIdAsync(5)).ReturnsAsync(CreateQuiz());
        userRepository.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(new User { UserId = 15 });
        questionRepository.Setup(r => r.GetByQuizIdAsync(5)).ReturnsAsync(Array.Empty<QuizQuestion>());

        var action = () => BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(5, "clerk_001", new CreateQuizAttemptDto());

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("This quiz does not contain any active questions.");
    }

    [Fact(DisplayName = "SubmitAttemptAsync throws when the submission omits required answers")]
    public async Task SubmitAttemptAsync_AnswerCountMismatch_ThrowsArgumentException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        quizRepository.Setup(r => r.GetActiveByIdAsync(5)).ReturnsAsync(CreateQuiz());
        userRepository.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(new User { UserId = 15 });
        questionRepository.Setup(r => r.GetByQuizIdAsync(5)).ReturnsAsync(new[]
        {
            CreateQuestion(1, "A", 10),
            CreateQuestion(2, "B", 15)
        });

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" }
            }
        };

        var action = () => BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(5, "clerk_001", dto);

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Exactly 2 answers are required for quiz 5.");
    }

    [Fact(DisplayName = "SubmitAttemptAsync throws when a question is answered more than once")]
    public async Task SubmitAttemptAsync_DuplicateQuestionIds_ThrowsArgumentException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        quizRepository.Setup(r => r.GetActiveByIdAsync(5)).ReturnsAsync(CreateQuiz());
        userRepository.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(new User { UserId = 15 });
        questionRepository.Setup(r => r.GetByQuizIdAsync(5)).ReturnsAsync(new[]
        {
            CreateQuestion(1, "A", 10),
            CreateQuestion(2, "B", 15)
        });

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },
                new() { QuestionId = 1, SelectedOption = "B" }
            }
        };

        var action = () => BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(5, "clerk_001", dto);

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("Each question may only be answered once per attempt.");
    }

    [Fact(DisplayName = "SubmitAttemptAsync throws when answers include a question from another quiz")]
    public async Task SubmitAttemptAsync_InvalidQuestionIds_ThrowsArgumentException()
    {
        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        quizRepository.Setup(r => r.GetActiveByIdAsync(5)).ReturnsAsync(CreateQuiz());
        userRepository.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(new User { UserId = 15 });
        questionRepository.Setup(r => r.GetByQuizIdAsync(5)).ReturnsAsync(new[]
        {
            CreateQuestion(1, "A", 10),
            CreateQuestion(2, "B", 15)
        });

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },
                new() { QuestionId = 99, SelectedOption = "B" }
            }
        };

        var action = () => BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(5, "clerk_001", dto);

        await action.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("One or more submitted questions do not belong to quiz 5.");
    }

    [Fact(DisplayName = "SubmitAttemptAsync grades the first attempt, awards XP, and unlocks badges")]
    public async Task SubmitAttemptAsync_FirstAttempt_AwardsXpAndBadges()
    {
        const int quizId = 5;
        const int userId = 15;

        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        QuizAttempt? persistedAttempt = null;
        List<AttemptAnswer>? persistedAnswers = null;
        int persistedXpAward = -1;

        quizRepository.Setup(r => r.GetActiveByIdAsync(quizId)).ReturnsAsync(CreateQuiz(quizId, passScore: 70));
        userRepository.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(new User { UserId = userId, ClerkUserId = "clerk_001" });
        questionRepository.Setup(r => r.GetByQuizIdAsync(quizId)).ReturnsAsync(new[]
        {
            CreateQuestion(1, "A", 10, "A explanation"),
            CreateQuestion(2, "B", 15, "B explanation")
        });
        attemptRepository.Setup(r => r.HasExistingAttemptAsync(userId, quizId)).ReturnsAsync(false);
        attemptRepository
            .Setup(r => r.SubmitAttemptTransactionalAsync(It.IsAny<QuizAttempt>(), It.IsAny<IEnumerable<AttemptAnswer>>(), It.IsAny<int>()))
            .Callback<QuizAttempt, IEnumerable<AttemptAnswer>, int>((attempt, answers, xpToAward) =>
            {
                persistedAttempt = attempt;
                persistedAnswers = answers.ToList();
                persistedXpAward = xpToAward;
            })
            .ReturnsAsync((QuizAttempt attempt, IEnumerable<AttemptAnswer> _, int _) =>
            {
                attempt.AttemptId = 501;
                return attempt;
            });
        badgeService
            .Setup(s => s.AwardUnlockedBadgesAsync(userId))
            .ReturnsAsync(Array.Empty<BadgeResponseDto>());

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "a" },
                new() { QuestionId = 2, SelectedOption = "B" }
            }
        };

        var result = await BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(quizId, "clerk_001", dto);

        result.AttemptId.Should().Be(501);
        result.QuizId.Should().Be(quizId);
        result.Score.Should().Be(100);
        result.CorrectCount.Should().Be(2);
        result.TotalQuestions.Should().Be(2);
        result.Passed.Should().BeTrue();
        result.XpEarned.Should().Be(25);
        result.IsFirstAttempt.Should().BeTrue();
        result.Results.Should().HaveCount(2);
        result.Results.Select(r => r.IsCorrect).Should().OnlyContain(isCorrect => isCorrect);

        persistedAttempt.Should().NotBeNull();
        persistedAttempt!.UserId.Should().Be(userId);
        persistedAttempt.QuizId.Should().Be(quizId);
        persistedAttempt.Score.Should().Be(2);
        persistedAttempt.TotalQuestions.Should().Be(2);
        persistedAttempt.XpEarned.Should().Be(25);
        persistedAttempt.Passed.Should().BeTrue();

        persistedAnswers.Should().NotBeNull();
        persistedAnswers!.Should().HaveCount(2);
        persistedAnswers.Select(a => a.QuestionId).Should().Equal(1, 2);
        persistedAnswers.Select(a => a.IsCorrect).Should().OnlyContain(isCorrect => isCorrect);

        persistedXpAward.Should().Be(25);
        badgeService.Verify(s => s.AwardUnlockedBadgesAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "SubmitAttemptAsync gives retry attempts a score without awarding extra XP or badges")]
    public async Task SubmitAttemptAsync_RetryAttempt_SkipsXpAndBadgeAward()
    {
        const int quizId = 5;
        const int userId = 15;

        var quizRepository = new Mock<IQuizRepository>();
        var questionRepository = new Mock<IQuizQuestionRepository>();
        var userRepository = new Mock<IUserRepository>();
        var attemptRepository = new Mock<IQuizAttemptRepository>();
        var badgeService = new Mock<IBadgeService>();

        int persistedXpAward = -1;

        quizRepository.Setup(r => r.GetActiveByIdAsync(quizId)).ReturnsAsync(CreateQuiz(quizId, passScore: 80));
        userRepository.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(new User { UserId = userId, ClerkUserId = "clerk_001" });
        questionRepository.Setup(r => r.GetByQuizIdAsync(quizId)).ReturnsAsync(new[]
        {
            CreateQuestion(1, "A", 10),
            CreateQuestion(2, "B", 15)
        });
        attemptRepository.Setup(r => r.HasExistingAttemptAsync(userId, quizId)).ReturnsAsync(true);
        attemptRepository
            .Setup(r => r.SubmitAttemptTransactionalAsync(It.IsAny<QuizAttempt>(), It.IsAny<IEnumerable<AttemptAnswer>>(), It.IsAny<int>()))
            .Callback<QuizAttempt, IEnumerable<AttemptAnswer>, int>((_, _, xpToAward) => persistedXpAward = xpToAward)
            .ReturnsAsync((QuizAttempt attempt, IEnumerable<AttemptAnswer> _, int _) =>
            {
                attempt.AttemptId = 777;
                return attempt;
            });

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },
                new() { QuestionId = 2, SelectedOption = "C" }
            }
        };

        var result = await BuildSut(quizRepository, questionRepository, userRepository, attemptRepository, badgeService)
            .SubmitAttemptAsync(quizId, "clerk_001", dto);

        result.AttemptId.Should().Be(777);
        result.Score.Should().Be(50);
        result.CorrectCount.Should().Be(1);
        result.TotalQuestions.Should().Be(2);
        result.Passed.Should().BeFalse();
        result.XpEarned.Should().Be(0);
        result.IsFirstAttempt.Should().BeFalse();
        persistedXpAward.Should().Be(0);

        badgeService.Verify(s => s.AwardUnlockedBadgesAsync(It.IsAny<int>()), Times.Never);
    }
}
