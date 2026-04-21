using backend.Data;
using backend.Models;
using backend.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace UnitTests.Repositories;

internal static class RepositoryTestHelpers
{
    public static IConfiguration FakeConfig => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=fake;Database=fake;User=fake;Password=fake;"
        })
        .Build();

    public static Mock<DatabaseHelper> BuildFailingDbMock()
    {
        var dbMock = new Mock<DatabaseHelper>(FakeConfig);
        dbMock
            .Setup(d => d.OpenConnectionAsync())
            .ThrowsAsync(new InvalidOperationException("Simulated DB connection failure."));

        return dbMock;
    }
}

public class QuizRepositoryTests
{
    private static QuizRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "QuizRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new QuizRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "QuizRepository propagates DB failures for primary data access methods")]
    public async Task CoreMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();

        await repo.Invoking(r => r.GetActiveAsync()).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetAllAsync()).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetByIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetActiveByIdAsync(1)).Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "QuizRepository propagates DB failures for mutating methods")]
    public async Task MutatingMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();
        var quiz = new Quiz { QuizId = 1, AlgorithmId = 2, CreatedBy = 3, Title = "Quiz", PassScore = 70, IsActive = true };

        await repo.Invoking(r => r.CreateAsync(quiz)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.UpdateAsync(quiz)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.DeleteAsync(1)).Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "QuizRepository GetByIdsAsync returns empty without touching the database when ids are empty")]
    public async Task GetByIdsAsync_EmptyIds_ReturnsEmpty()
    {
        var dbMock = RepositoryTestHelpers.BuildFailingDbMock();
        var repo = new QuizRepository(dbMock.Object);

        var result = await repo.GetByIdsAsync(Array.Empty<int>());

        result.Should().BeEmpty();
        dbMock.Verify(d => d.OpenConnectionAsync(), Times.Never);
    }

    [Fact(DisplayName = "QuizRepository GetByIdsAsync propagates DB failures for non-empty ids")]
    public async Task GetByIdsAsync_NonEmptyIds_PropagatesException()
    {
        var repo = BuildRepo();

        await repo.Invoking(r => r.GetByIdsAsync([1, 2])).Should().ThrowAsync<Exception>();
    }
}

public class QuizAttemptRepositoryTests
{
    private static QuizAttemptRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "QuizAttemptRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new QuizAttemptRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "QuizAttemptRepository propagates DB failures for attempt write operations")]
    public async Task WriteMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();
        var attempt = new QuizAttempt
        {
            UserId = 1,
            QuizId = 2,
            Score = 1,
            TotalQuestions = 2,
            XpEarned = 10,
            Passed = true,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        var answer = new AttemptAnswer { AttemptId = 1, QuestionId = 5, SelectedOption = "A", IsCorrect = true };

        await repo.Invoking(r => r.CreateAttemptAsync(attempt)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.CreateAnswerAsync(answer)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.CreateAnswersAsync([answer])).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.SubmitAttemptTransactionalAsync(attempt, [answer], 10)).Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "QuizAttemptRepository CreateAnswersAsync returns empty without touching the database when there are no answers")]
    public async Task CreateAnswersAsync_EmptyAnswers_ReturnsEmpty()
    {
        var dbMock = RepositoryTestHelpers.BuildFailingDbMock();
        var repo = new QuizAttemptRepository(dbMock.Object);

        var result = await repo.CreateAnswersAsync(Array.Empty<AttemptAnswer>());

        result.Should().BeEmpty();
        dbMock.Verify(d => d.OpenConnectionAsync(), Times.Never);
    }

    [Fact(DisplayName = "QuizAttemptRepository propagates DB failures for read operations")]
    public async Task ReadMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();

        await repo.Invoking(r => r.HasExistingAttemptAsync(1, 2)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetAllAsync()).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetAttemptsForUserAsync(1, 1, 10)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetAttemptHistoryByUserIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetAlgorithmCoverageByUserIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetPerformanceSummaryByUserIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetRecentActivityAsync(1, 10)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetDailyActivityAsync(1)).Should().ThrowAsync<Exception>();
    }
}

public class QuizQuestionRepositoryTests
{
    private static QuizQuestionRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "QuizQuestionRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new QuizQuestionRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "QuizQuestionRepository propagates DB failures for all public methods")]
    public async Task PublicMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();
        var question = new QuizQuestion
        {
            QuestionId = 1,
            QuizId = 2,
            QuestionText = "What is O(n)?",
            OptionA = "A",
            OptionB = "B",
            OptionC = "C",
            OptionD = "D",
            CorrectOption = "B",
            Difficulty = "easy",
            XpReward = 10,
            OrderIndex = 1,
            IsActive = true
        };

        await repo.Invoking(r => r.GetByQuizIdAsync(2)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetByIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.CreateAsync(question)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.UpdateAsync(question)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.DeleteAsync(1)).Should().ThrowAsync<Exception>();
    }
}

public class BadgeRepositoryTests
{
    private static BadgeRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "BadgeRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new BadgeRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "BadgeRepository propagates DB failures for public methods that require a connection")]
    public async Task PublicMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();
        var badge = new Badge
        {
            BadgeId = 1,
            BadgeName = "Bronze",
            BadgeDescription = "First milestone",
            XpThreshold = 100,
            IconType = "star",
            IconColor = "#123456",
            UnlockHint = "Keep going"
        };

        await repo.Invoking(r => r.GetAllAsync()).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetByIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetEarnedBadgesByUserIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetUnlockedBadgesByUserIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.CreateAsync(badge)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.UpdateAsync(1, badge)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.DeleteAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.AwardBadgeToUserAsync(1, 1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetUnlockedAlgorithmBadgesByUserIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetEarnedBadgesWithAwardDateAsync(1)).Should().ThrowAsync<Exception>();
    }
}

public class AlgorithmRepositoryTests
{
    private static AlgorithmRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "AlgorithmRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new AlgorithmRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "AlgorithmRepository propagates DB failures for standard queries")]
    public async Task StandardQueries_DbFailure_PropagateException()
    {
        var repo = BuildRepo();

        await repo.Invoking(r => r.GetAllAsync()).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetByIdAsync(1)).Should().ThrowAsync<Exception>();
    }

    [Fact(DisplayName = "AlgorithmRepository GetByIdsAsync returns empty without touching the database when ids are empty")]
    public async Task GetByIdsAsync_EmptyIds_ReturnsEmpty()
    {
        var dbMock = RepositoryTestHelpers.BuildFailingDbMock();
        var repo = new AlgorithmRepository(dbMock.Object);

        var result = await repo.GetByIdsAsync(Array.Empty<int>());

        result.Should().BeEmpty();
        dbMock.Verify(d => d.OpenConnectionAsync(), Times.Never);
    }

    [Fact(DisplayName = "AlgorithmRepository GetByIdsAsync propagates DB failures for non-empty ids")]
    public async Task GetByIdsAsync_NonEmptyIds_PropagatesException()
    {
        var repo = BuildRepo();

        await repo.Invoking(r => r.GetByIdsAsync([1, 2])).Should().ThrowAsync<Exception>();
    }
}

public class CodingQuestionRepositoryTests
{
    private static CodingQuestionRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "CodingQuestionRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new CodingQuestionRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "CodingQuestionRepository propagates DB failures for all public methods")]
    public async Task PublicMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();
        var question = new CodingQuestion
        {
            Id = 1,
            Title = "Reverse String",
            Description = "Return the reversed string",
            InputExample = "abc",
            ExpectedOutput = "cba",
            Difficulty = "easy"
        };

        await repo.Invoking(r => r.GetAllAsync()).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetByIdAsync(1)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.CreateAsync(question)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.UpdateAsync(question)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.DeleteAsync(1)).Should().ThrowAsync<Exception>();
    }
}

public class ReportRepositoryTests
{
    private static ReportRepository BuildRepo() => new(RepositoryTestHelpers.BuildFailingDbMock().Object);

    [Fact(DisplayName = "ReportRepository constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var repo = new ReportRepository(new Mock<DatabaseHelper>(RepositoryTestHelpers.FakeConfig).Object);

        repo.Should().NotBeNull();
    }

    [Fact(DisplayName = "ReportRepository propagates DB failures for all report queries")]
    public async Task PublicMethods_DbFailure_PropagateException()
    {
        var repo = BuildRepo();
        var start = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Utc);

        await repo.Invoking(r => r.GetPerStudentReportAsync(start, end)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetPerAlgorithmReportAsync(start, end)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetPerQuizReportAsync(start, end)).Should().ThrowAsync<Exception>();
        await repo.Invoking(r => r.GetSummaryStatisticsAsync(start, end)).Should().ThrowAsync<Exception>();
    }
}
