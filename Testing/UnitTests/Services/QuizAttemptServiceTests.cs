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
/// Unit tests for <see cref="QuizAttemptService"/>.
///
/// Scenarios covered
/// -----------------
/// SubmitAttemptAsync — pre-condition guards
///   1. Throws KeyNotFoundException when quiz not found or inactive
///   2. Throws KeyNotFoundException when user has no local account
///   3. Throws ArgumentException when quiz has no active questions
///   4. Throws ArgumentException when answer count does not match question count
///   5. Throws ArgumentException when duplicate question IDs are submitted
///   6. Throws ArgumentException when a submitted question does not belong to the quiz
/// SubmitAttemptAsync — grading and persistence
///   7. Awards XP only on first attempt; retry attempt yields XpEarned = 0
///   8. Calculates correct score percentage (rounded)
///   9. Marks attempt as passed when score >= passScore
///  10. Marks attempt as failed when score < passScore
///  11. Includes per-question result with correct CorrectOption and IsCorrect values
///  12. XP earned equals sum of XpReward for correctly answered questions only
///  13. Passes xpToAward=0 to transactional repo on retry (no double-award)
///  14. IsFirstAttempt = true on first submission, false on retry
/// </summary>
public class QuizAttemptServiceTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Quiz SampleQuiz(int passScore = 70) => new()
    {
        QuizId    = 1,
        Title     = "Test Quiz",
        IsActive  = true,
        PassScore = passScore
    };

    private static User SampleUser() => new()
    {
        UserId      = 5,
        ClerkUserId = "clerk_001",
        Email       = "student@example.com",
        Username    = "student",
        Role        = "User"
    };

    /// <summary>Creates a list of n questions with sequential IDs, each worth 10 XP.</summary>
    private static List<QuizQuestion> MakeQuestions(int count, string correctOption = "A") =>
        Enumerable.Range(1, count)
                  .Select(i => new QuizQuestion
                  {
                      QuestionId    = i,
                      QuizId        = 1,
                      QuestionType  = "MCQ",
                      QuestionText  = $"Question {i}",
                      OptionA = "Opt A", OptionB = "Opt B", OptionC = "Opt C", OptionD = "Opt D",
                      CorrectOption = correctOption,
                      Difficulty    = "easy",
                      XpReward      = 10,
                      IsActive      = true
                  })
                  .ToList();

    /// <summary>Creates a submission DTO with every question answered with the given option.</summary>
    private static CreateQuizAttemptDto MakeSubmission(
        IEnumerable<QuizQuestion> questions,
        string selectedOption = "A") => new()
    {
        Answers = questions
            .Select(q => new QuizAttemptAnswerSubmissionDto
            {
                QuestionId     = q.QuestionId,
                SelectedOption = selectedOption
            })
            .ToList()
    };

    private static QuizAttemptService BuildSut(
        Mock<IQuizRepository>         quizRepo,
        Mock<IQuizQuestionRepository> qRepo,
        Mock<IUserRepository>         userRepo,
        Mock<IQuizAttemptRepository>  attemptRepo) =>
        new(quizRepo.Object, qRepo.Object, userRepo.Object, attemptRepo.Object,
            NullLogger<QuizAttemptService>.Instance);

    /// <summary>
    /// Returns a mock IQuizAttemptRepository configured to echo the attempt
    /// back with AttemptId = 99 so tests can assert on the returned object.
    /// </summary>
    private static Mock<IQuizAttemptRepository> MakeAttemptRepo(bool hasExisting = false)
    {
        var repo = new Mock<IQuizAttemptRepository>();
        repo.Setup(r => r.HasExistingAttemptAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(hasExisting);
        repo.Setup(r => r.SubmitAttemptTransactionalAsync(
                It.IsAny<QuizAttempt>(),
                It.IsAny<IEnumerable<AttemptAnswer>>(),
                It.IsAny<int>()))
            .ReturnsAsync((QuizAttempt a, IEnumerable<AttemptAnswer> _, int _) =>
            {
                a.AttemptId = 99;
                return a;
            });
        return repo;
    }

    // -----------------------------------------------------------------------
    // Pre-condition guards
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — SubmitAttemptAsync: throws KeyNotFoundException when quiz not found")]
    public async Task Should_ThrowKeyNotFound_When_QuizNotFound()
    {
        // Arrange
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync((Quiz?)null);

        var dto = MakeSubmission(MakeQuestions(1));

        // Act & Assert
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .Invoking(s => s.SubmitAttemptAsync(1, "clerk_001", dto))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact(DisplayName = "Scenario 2 — SubmitAttemptAsync: throws KeyNotFoundException when user not synced")]
    public async Task Should_ThrowKeyNotFound_When_UserNotSynced()
    {
        // Arrange
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync((User?)null);

        var dto = MakeSubmission(MakeQuestions(1));

        // Act & Assert
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .Invoking(s => s.SubmitAttemptAsync(1, "clerk_001", dto))
            .Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*local account*");
    }

    [Fact(DisplayName = "Scenario 3 — SubmitAttemptAsync: throws ArgumentException when quiz has no active questions")]
    public async Task Should_ThrowArgument_When_QuizHasNoActiveQuestions()
    {
        // Arrange
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(Array.Empty<QuizQuestion>());

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" }
            }
        };

        // Act & Assert
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .Invoking(s => s.SubmitAttemptAsync(1, "clerk_001", dto))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*active questions*");
    }

    [Fact(DisplayName = "Scenario 4 — SubmitAttemptAsync: throws ArgumentException when answer count mismatches")]
    public async Task Should_ThrowArgument_When_AnswerCountDoesNotMatchQuestionCount()
    {
        // Arrange
        var questions   = MakeQuestions(3);
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        // Only 2 answers for 3 questions
        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },
                new() { QuestionId = 2, SelectedOption = "B" }
            }
        };

        // Act & Assert
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .Invoking(s => s.SubmitAttemptAsync(1, "clerk_001", dto))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*3 answers*");
    }

    [Fact(DisplayName = "Scenario 5 — SubmitAttemptAsync: throws ArgumentException when duplicate question IDs submitted")]
    public async Task Should_ThrowArgument_When_DuplicateQuestionIdsSubmitted()
    {
        // Arrange
        var questions   = MakeQuestions(2);
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        // Question 1 submitted twice
        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },
                new() { QuestionId = 1, SelectedOption = "B" }   // duplicate
            }
        };

        // Act & Assert
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .Invoking(s => s.SubmitAttemptAsync(1, "clerk_001", dto))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*once*");
    }

    [Fact(DisplayName = "Scenario 6 — SubmitAttemptAsync: throws ArgumentException when question does not belong to quiz")]
    public async Task Should_ThrowArgument_When_SubmittedQuestionNotInQuiz()
    {
        // Arrange
        var questions   = MakeQuestions(1); // question IDs: [1]
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        // Submits question ID 999 which is not in the quiz
        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 999, SelectedOption = "A" }
            }
        };

        // Act & Assert
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .Invoking(s => s.SubmitAttemptAsync(1, "clerk_001", dto))
            .Should().ThrowAsync<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // Grading and persistence
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 7 — SubmitAttemptAsync: XP awarded only on first attempt")]
    public async Task Should_AwardXpOnFirstAttemptOnly()
    {
        // Arrange
        var questions = MakeQuestions(2, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        // First attempt
        var firstAttemptRepo = MakeAttemptRepo(hasExisting: false);
        var dto = MakeSubmission(questions, "A"); // all correct

        var firstResult = await BuildSut(quizRepo, qRepo, userRepo, firstAttemptRepo)
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Second attempt
        var retryAttemptRepo = MakeAttemptRepo(hasExisting: true);

        var retryResult = await BuildSut(quizRepo, qRepo, userRepo, retryAttemptRepo)
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert
        firstResult.XpEarned.Should().BeGreaterThan(0);
        firstResult.IsFirstAttempt.Should().BeTrue();

        retryResult.XpEarned.Should().Be(0);
        retryResult.IsFirstAttempt.Should().BeFalse();
    }

    [Fact(DisplayName = "Scenario 8 — SubmitAttemptAsync: calculates score percentage correctly")]
    public async Task Should_CalculateCorrectScorePercentage()
    {
        // Arrange — 3 questions, correct answer is "A"; student answers 2 correct, 1 wrong
        var questions = MakeQuestions(3, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },  // correct
                new() { QuestionId = 2, SelectedOption = "A" },  // correct
                new() { QuestionId = 3, SelectedOption = "B" }   // wrong
            }
        };

        var attemptRepo = MakeAttemptRepo();

        // Act
        var result = await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert — 2/3 = 66.67% → rounds to 67
        result.Score.Should().Be(67);
        result.CorrectCount.Should().Be(2);
        result.TotalQuestions.Should().Be(3);
    }

    [Fact(DisplayName = "Scenario 9 — SubmitAttemptAsync: marks attempt as passed when score >= passScore")]
    public async Task Should_MarkPassed_When_ScoreAtOrAbovePassThreshold()
    {
        // Arrange — 2 questions, passScore = 50; student answers both correctly → 100%
        var questions = MakeQuestions(2, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz(passScore: 50));
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = MakeSubmission(questions, "A"); // all correct

        // Act
        var result = await BuildSut(quizRepo, qRepo, userRepo, MakeAttemptRepo())
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert
        result.Passed.Should().BeTrue();
    }

    [Fact(DisplayName = "Scenario 10 — SubmitAttemptAsync: marks attempt as failed when score < passScore")]
    public async Task Should_MarkFailed_When_ScoreBelowPassThreshold()
    {
        // Arrange — 2 questions, passScore = 70; student answers 0 correctly → 0%
        var questions = MakeQuestions(2, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz(passScore: 70));
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = MakeSubmission(questions, "D"); // all wrong

        // Act
        var result = await BuildSut(quizRepo, qRepo, userRepo, MakeAttemptRepo())
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert
        result.Passed.Should().BeFalse();
    }

    [Fact(DisplayName = "Scenario 11 — SubmitAttemptAsync: includes per-question results with correct IsCorrect flag")]
    public async Task Should_IncludePerQuestionResults_WithCorrectIsCorrectFlag()
    {
        // Arrange — 2 questions; Q1 correct="A" (student picks "A"), Q2 correct="A" (student picks "B")
        var questions = MakeQuestions(2, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" }, // correct
                new() { QuestionId = 2, SelectedOption = "B" }  // wrong
            }
        };

        // Act
        var result = await BuildSut(quizRepo, qRepo, userRepo, MakeAttemptRepo())
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert
        result.Results.Should().HaveCount(2);
        result.Results.First(r => r.QuestionId == 1).IsCorrect.Should().BeTrue();
        result.Results.First(r => r.QuestionId == 2).IsCorrect.Should().BeFalse();
        result.Results.First(r => r.QuestionId == 1).CorrectOption.Should().Be("A");
    }

    [Fact(DisplayName = "Scenario 12 — SubmitAttemptAsync: XP sum covers only correctly answered questions")]
    public async Task Should_SumXpFromCorrectAnswersOnly()
    {
        // Arrange — 3 questions worth 10 XP each; student gets 2 right
        var questions = MakeQuestions(3, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz(passScore: 50));
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = new CreateQuizAttemptDto
        {
            Answers = new List<QuizAttemptAnswerSubmissionDto>
            {
                new() { QuestionId = 1, SelectedOption = "A" },  // correct → 10 XP
                new() { QuestionId = 2, SelectedOption = "A" },  // correct → 10 XP
                new() { QuestionId = 3, SelectedOption = "D" }   // wrong   →  0 XP
            }
        };

        // Act
        var result = await BuildSut(quizRepo, qRepo, userRepo, MakeAttemptRepo(hasExisting: false))
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert — 2 correct × 10 XP each = 20 XP
        result.XpEarned.Should().Be(20);
    }

    [Fact(DisplayName = "Scenario 13 — SubmitAttemptAsync: passes xpToAward=0 to repository on retry")]
    public async Task Should_PassZeroXpToRepository_When_RetryAttempt()
    {
        // Arrange
        var questions   = MakeQuestions(1, correctOption: "A");
        var quizRepo    = new Mock<IQuizRepository>();
        var qRepo       = new Mock<IQuizQuestionRepository>();
        var userRepo    = new Mock<IUserRepository>();
        var attemptRepo = MakeAttemptRepo(hasExisting: true); // already has attempt

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = MakeSubmission(questions, "A"); // correct

        // Act
        await BuildSut(quizRepo, qRepo, userRepo, attemptRepo)
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert — xpToAward must be 0 on retry
        attemptRepo.Verify(r => r.SubmitAttemptTransactionalAsync(
            It.IsAny<QuizAttempt>(),
            It.IsAny<IEnumerable<AttemptAnswer>>(),
            0), Times.Once);
    }

    [Fact(DisplayName = "Scenario 14 — SubmitAttemptAsync: IsFirstAttempt reflects prior attempt status")]
    public async Task Should_SetIsFirstAttemptCorrectly_BasedOnPriorAttempt()
    {
        // Arrange
        var questions = MakeQuestions(1, correctOption: "A");
        var quizRepo  = new Mock<IQuizRepository>();
        var qRepo     = new Mock<IQuizQuestionRepository>();
        var userRepo  = new Mock<IUserRepository>();

        quizRepo.Setup(r => r.GetActiveByIdAsync(1)).ReturnsAsync(SampleQuiz());
        userRepo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());
        qRepo.Setup(r => r.GetByQuizIdAsync(1)).ReturnsAsync(questions);

        var dto = MakeSubmission(questions, "A");

        // First attempt
        var firstResult = await BuildSut(quizRepo, qRepo, userRepo, MakeAttemptRepo(hasExisting: false))
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Retry
        var retryResult = await BuildSut(quizRepo, qRepo, userRepo, MakeAttemptRepo(hasExisting: true))
            .SubmitAttemptAsync(1, "clerk_001", dto);

        // Assert
        firstResult.IsFirstAttempt.Should().BeTrue();
        retryResult.IsFirstAttempt.Should().BeFalse();
    }
}
