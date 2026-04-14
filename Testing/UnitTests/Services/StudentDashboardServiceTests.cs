using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class StudentDashboardServiceTests
{
    private static StudentDashboardService BuildSut(
        Mock<IUserRepository> userRepository,
        Mock<ILevelingService> levelingService,
        Mock<IBadgeService> badgeService,
        Mock<IQuizAttemptRepository> quizAttemptRepository)
        => new(
            userRepository.Object,
            levelingService.Object,
            badgeService.Object,
            quizAttemptRepository.Object);

    private static User BuildUser(int userId, int xpTotal)
        => new()
        {
            UserId = userId,
            ClerkUserId = $"clerk_{userId}",
            Email = $"student{userId}@example.com",
            Username = $"student_{userId}",
            Role = "User",
            XpTotal = xpTotal,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    [Fact(DisplayName = "Scenario 1 - Missing user returns null and skips downstream aggregation")]
    public async Task GetStudentDashboardAsync_UserMissing_ReturnsNullAndSkipsDownstreamCalls()
    {
        const int userId = 404;


        var userRepository = new Mock<IUserRepository>();
        var levelingService = new Mock<ILevelingService>();
        var badgeService = new Mock<IBadgeService>();
        var quizAttemptRepository = new Mock<IQuizAttemptRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((User?)null);

        var sut = BuildSut(userRepository, levelingService, badgeService, quizAttemptRepository);

        var result = await sut.GetStudentDashboardAsync(userId);

        result.Should().BeNull();
        badgeService.Verify(s => s.AwardUnlockedBadgesAsync(It.IsAny<int>()), Times.Never);
        badgeService.Verify(s => s.GetEarnedBadgesWithAwardDateAsync(It.IsAny<int>()), Times.Never);
        badgeService.Verify(s => s.GetAllBadgesAsync(), Times.Never);
        quizAttemptRepository.Verify(r => r.GetPerformanceSummaryByUserIdAsync(It.IsAny<int>()), Times.Never);
        quizAttemptRepository.Verify(r => r.GetAttemptHistoryByUserIdAsync(It.IsAny<int>()), Times.Never);
        quizAttemptRepository.Verify(r => r.GetAlgorithmCoverageByUserIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "Scenario 2 - Aggregates progression, badges, and quiz analytics into dashboard DTO")]
    public async Task GetStudentDashboardAsync_ValidUser_ReturnsComposedDashboard()
    {
        const int userId = 42;
        const int xpTotal = 250;

        var userRepository = new Mock<IUserRepository>();
        var levelingService = new Mock<ILevelingService>();
        var badgeService = new Mock<IBadgeService>();
        var quizAttemptRepository = new Mock<IQuizAttemptRepository>();

        var user = BuildUser(userId, xpTotal);
        var earnedBadges = new List<EarnedBadgeDto>
        {
            new()
            {
                Id = 1,
                Name = "First Steps",
                Description = "Earned at 50 XP",
                XpThreshold = 50,
                IconType = "star",
                IconColor = "#f6c945",
                AwardDate = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc)
            }
        };

        var allBadges = new List<BadgeResponseDto>
        {
            new()
            {
                BadgeId = 1,
                BadgeName = "First Steps",
                BadgeDescription = "Earned at 50 XP",
                XpThreshold = 50,
                IconType = "star",
                IconColor = "#f6c945",
                UnlockHint = "Reach 50 XP"
            },
            new()
            {
                BadgeId = 2,
                BadgeName = "Quick Learner",
                BadgeDescription = "Earned at 150 XP",
                XpThreshold = 150,
                IconType = "bolt",
                IconColor = "#7df9ff",
                UnlockHint = "Reach 150 XP"
            }
        };

        var performanceSummary = new PerformanceSummaryDto
        {
            TotalAttempts = 5,
            TotalPassed = 4,
            PassRate = 80,
            AverageScore = 82.5,
            TotalXpFromQuizzes = 180
        };

        var history = new List<QuizAttemptHistoryItemDto>
        {
            new()
            {
                AttemptId = 1001,
                QuizId = 77,
                QuizTitle = "Quick Sort Quiz",
                AlgorithmName = "Quick Sort",
                Score = 8,
                TotalQuestions = 10,
                ScorePercent = 80,
                XpEarned = 40,
                Passed = true,
                CompletedAt = new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc)
            }
        };

        var coverage = new List<AlgorithmCoverageItemDto>
        {
            new()
            {
                AlgorithmId = 7,
                AlgorithmName = "Quick Sort",
                Category = "Sorting",
                TotalAttempts = 3,
                PassedAttempts = 2,
                BestScorePercent = 90,
                HasPassedQuiz = true
            }
        };

        var sequence = new MockSequence();
        userRepository
            .InSequence(sequence)
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(user);
        badgeService
            .InSequence(sequence)
            .Setup(s => s.AwardUnlockedBadgesAsync(userId))
            .ReturnsAsync(Array.Empty<BadgeResponseDto>());
        badgeService
            .InSequence(sequence)
            .Setup(s => s.GetEarnedBadgesWithAwardDateAsync(userId))
            .ReturnsAsync(earnedBadges);
        badgeService
            .InSequence(sequence)
            .Setup(s => s.GetAllBadgesAsync())
            .ReturnsAsync(allBadges);

        levelingService
            .Setup(s => s.CalculateLevel(xpTotal))
            .Returns(3);
        levelingService
            .Setup(s => s.GetXpForPreviousLevel(3))
            .Returns(200);
        levelingService
            .Setup(s => s.GetXpForNextLevel(3))
            .Returns(300);

        quizAttemptRepository
            .Setup(r => r.GetPerformanceSummaryByUserIdAsync(userId))
            .ReturnsAsync(performanceSummary);
        quizAttemptRepository
            .Setup(r => r.GetAttemptHistoryByUserIdAsync(userId))
            .ReturnsAsync(history);
        quizAttemptRepository
            .Setup(r => r.GetAlgorithmCoverageByUserIdAsync(userId))
            .ReturnsAsync(coverage);

        var sut = BuildSut(userRepository, levelingService, badgeService, quizAttemptRepository);

        var result = await sut.GetStudentDashboardAsync(userId);

        result.Should().NotBeNull();
        result!.StudentId.Should().Be(userId);
        result.XpTotal.Should().Be(xpTotal);

        result.Progression.CurrentLevel.Should().Be(3);
        result.Progression.XpPrevLevel.Should().Be(200);
        result.Progression.XpForNextLevel.Should().Be(300);
        result.Progression.XpInCurrentLevel.Should().Be(50);
        result.Progression.XpNeededForLevel.Should().Be(100);
        result.Progression.ProgressPercentage.Should().BeApproximately(50.0, 0.0001);

        result.EarnedBadges.Should().BeEquivalentTo(earnedBadges);
        var allBadgesDashboard = result.AllBadges.OrderBy(b => b.Id).ToList();
        allBadgesDashboard.Should().HaveCount(2);
        allBadgesDashboard.Single(b => b.Id == 1).Earned.Should().BeTrue();
        allBadgesDashboard.Single(b => b.Id == 2).Earned.Should().BeFalse();

        result.PerformanceSummary.Should().BeEquivalentTo(performanceSummary);
        result.QuizAttemptHistory.Should().BeEquivalentTo(history);
        result.AlgorithmCoverage.Should().BeEquivalentTo(coverage);

        badgeService.Verify(s => s.AwardUnlockedBadgesAsync(userId), Times.Once);
        badgeService.Verify(s => s.GetEarnedBadgesWithAwardDateAsync(userId), Times.Once);
        badgeService.Verify(s => s.GetAllBadgesAsync(), Times.Once);
        quizAttemptRepository.Verify(r => r.GetPerformanceSummaryByUserIdAsync(userId), Times.Once);
        quizAttemptRepository.Verify(r => r.GetAttemptHistoryByUserIdAsync(userId), Times.Once);
        quizAttemptRepository.Verify(r => r.GetAlgorithmCoverageByUserIdAsync(userId), Times.Once);
    }

    [Fact(DisplayName = "Scenario 3 - Handles zero XP span without divide-by-zero in progression")]
    public async Task GetStudentDashboardAsync_ZeroXpSpan_SetsProgressPercentageToZero()
    {
        const int userId = 7;
        const int xpTotal = 500;

        var userRepository = new Mock<IUserRepository>();
        var levelingService = new Mock<ILevelingService>();
        var badgeService = new Mock<IBadgeService>();
        var quizAttemptRepository = new Mock<IQuizAttemptRepository>();

        userRepository
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(BuildUser(userId, xpTotal));

        badgeService
            .Setup(s => s.AwardUnlockedBadgesAsync(userId))
            .ReturnsAsync(Array.Empty<BadgeResponseDto>());
        badgeService
            .Setup(s => s.GetEarnedBadgesWithAwardDateAsync(userId))
            .ReturnsAsync(Array.Empty<EarnedBadgeDto>());
        badgeService
            .Setup(s => s.GetAllBadgesAsync())
            .ReturnsAsync(Array.Empty<BadgeResponseDto>());

        levelingService
            .Setup(s => s.CalculateLevel(xpTotal))
            .Returns(6);
        levelingService
            .Setup(s => s.GetXpForPreviousLevel(6))
            .Returns(500);
        levelingService
            .Setup(s => s.GetXpForNextLevel(6))
            .Returns(500);

        quizAttemptRepository
            .Setup(r => r.GetPerformanceSummaryByUserIdAsync(userId))
            .ReturnsAsync(new PerformanceSummaryDto());
        quizAttemptRepository
            .Setup(r => r.GetAttemptHistoryByUserIdAsync(userId))
            .ReturnsAsync(Array.Empty<QuizAttemptHistoryItemDto>());
        quizAttemptRepository
            .Setup(r => r.GetAlgorithmCoverageByUserIdAsync(userId))
            .ReturnsAsync(Array.Empty<AlgorithmCoverageItemDto>());

        var sut = BuildSut(userRepository, levelingService, badgeService, quizAttemptRepository);

        var result = await sut.GetStudentDashboardAsync(userId);

        result.Should().NotBeNull();
        result!.Progression.XpNeededForLevel.Should().Be(0);
        result.Progression.ProgressPercentage.Should().Be(0);
    }
}