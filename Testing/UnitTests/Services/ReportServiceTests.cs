using backend.DTOs;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class ReportServiceTests
{
    private static ReportService BuildSut(Mock<IReportRepository> repo)
        => new(repo.Object, NullLogger<ReportService>.Instance);

    [Fact(DisplayName = "Scenario 1 - Full period returns combined report bundle with all sections populated")]
    public async Task GetAdminReportBundleAsync_FullPeriod_ReturnsCombinedBundle()
    {
        var repo = new Mock<IReportRepository>();
        var startDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc);

        var summary = new SummaryStatisticsDto
        {
            TotalAttempts = 12,
            TotalStudents = 4,
            AverageScore = 76.5m,
            TotalXp = 440
        };

        var perStudent = new List<PerStudentReportDto>
        {
            new()
            {
                StudentId = 1,
                StudentName = "alice",
                TotalAttempts = 5,
                AverageScore = 82.4m,
                BestScore = 96,
                TotalXp = 180,
                AlgorithmsAttempted = 3
            },
            new()
            {
                StudentId = 2,
                StudentName = "bob",
                TotalAttempts = 3,
                AverageScore = 68.7m,
                BestScore = 80,
                TotalXp = 90,
                AlgorithmsAttempted = 2
            }
        };

        var perAlgorithm = new List<PerAlgorithmReportDto>
        {
            new()
            {
                AlgorithmType = "Sorting",
                AttemptCount = 7,
                AverageScore = 74.1m,
                PassRate = 71.4m
            },
            new()
            {
                AlgorithmType = "Searching",
                AttemptCount = 5,
                AverageScore = 79.8m,
                PassRate = 80.0m
            }
        };

        var perQuiz = new List<PerQuizReportDto>
        {
            new()
            {
                Title = "Quick Sort Quiz",
                AttemptCount = 6,
                AverageScore = 78.2m,
                HighestScore = 96,
                LowestScore = 40
            },
            new()
            {
                Title = "Binary Search Quiz",
                AttemptCount = 6,
                AverageScore = 74.8m,
                HighestScore = 92,
                LowestScore = 48
            }
        };

        repo.Setup(r => r.GetSummaryStatisticsAsync(startDate, endDate)).ReturnsAsync(summary);
        repo.Setup(r => r.GetPerStudentReportAsync(startDate, endDate)).ReturnsAsync(perStudent);
        repo.Setup(r => r.GetPerAlgorithmReportAsync(startDate, endDate)).ReturnsAsync(perAlgorithm);
        repo.Setup(r => r.GetPerQuizReportAsync(startDate, endDate)).ReturnsAsync(perQuiz);

        var result = await BuildSut(repo).GetAdminReportBundleAsync(startDate, endDate);

        result.Summary.Should().BeEquivalentTo(summary);
        result.PerStudent.Should().BeEquivalentTo(perStudent);
        result.PerAlgorithm.Should().BeEquivalentTo(perAlgorithm);
        result.PerQuiz.Should().BeEquivalentTo(perQuiz);

        repo.Verify(r => r.GetSummaryStatisticsAsync(startDate, endDate), Times.Once);
        repo.Verify(r => r.GetPerStudentReportAsync(startDate, endDate), Times.Once);
        repo.Verify(r => r.GetPerAlgorithmReportAsync(startDate, endDate), Times.Once);
        repo.Verify(r => r.GetPerQuizReportAsync(startDate, endDate), Times.Once);
    }

    [Fact(DisplayName = "Scenario 2 - Empty period returns zero summary and empty breakdown collections")]
    public async Task GetAdminReportBundleAsync_EmptyPeriod_ReturnsEmptyDataShapes()
    {
        var repo = new Mock<IReportRepository>();
        var startDate = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);

        var summary = new SummaryStatisticsDto
        {
            TotalAttempts = 0,
            TotalStudents = 0,
            AverageScore = 0,
            TotalXp = 0
        };

        repo.Setup(r => r.GetSummaryStatisticsAsync(startDate, endDate)).ReturnsAsync(summary);
        repo.Setup(r => r.GetPerStudentReportAsync(startDate, endDate)).ReturnsAsync(Array.Empty<PerStudentReportDto>());
        repo.Setup(r => r.GetPerAlgorithmReportAsync(startDate, endDate)).ReturnsAsync(Array.Empty<PerAlgorithmReportDto>());
        repo.Setup(r => r.GetPerQuizReportAsync(startDate, endDate)).ReturnsAsync(Array.Empty<PerQuizReportDto>());

        var result = await BuildSut(repo).GetAdminReportBundleAsync(startDate, endDate);

        result.Summary.TotalAttempts.Should().Be(0);
        result.Summary.TotalStudents.Should().Be(0);
        result.Summary.AverageScore.Should().Be(0);
        result.Summary.TotalXp.Should().Be(0);
        result.PerStudent.Should().BeEmpty();
        result.PerAlgorithm.Should().BeEmpty();
        result.PerQuiz.Should().BeEmpty();
    }

    [Fact(DisplayName = "Scenario 3 - Boundary dates are forwarded exactly for summary aggregation")]
    public async Task GetSummaryStatisticsAsync_BoundaryDates_ForwardsExactRange()
    {
        var repo = new Mock<IReportRepository>();
        var startDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc).AddTicks(9999999);
        var expected = new SummaryStatisticsDto
        {
            TotalAttempts = 2,
            TotalStudents = 2,
            AverageScore = 65.0m,
            TotalXp = 40
        };

        repo.Setup(r => r.GetSummaryStatisticsAsync(startDate, endDate)).ReturnsAsync(expected);

        var result = await BuildSut(repo).GetSummaryStatisticsAsync(startDate, endDate);

        result.Should().BeEquivalentTo(expected);
        repo.Verify(r => r.GetSummaryStatisticsAsync(startDate, endDate), Times.Once);
    }

    [Fact(DisplayName = "Scenario 4 - Boundary dates are forwarded exactly for per-student aggregation")]
    public async Task GetPerStudentReportAsync_BoundaryDates_ForwardsExactRange()
    {
        var repo = new Mock<IReportRepository>();
        var startDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc).AddTicks(9999999);
        var expected = new[]
        {
            new PerStudentReportDto
            {
                StudentId = 10,
                StudentName = "boundary-student",
                TotalAttempts = 2,
                AverageScore = 75,
                BestScore = 80,
                TotalXp = 30,
                AlgorithmsAttempted = 2
            }
        };

        repo.Setup(r => r.GetPerStudentReportAsync(startDate, endDate)).ReturnsAsync(expected);

        var result = (await BuildSut(repo).GetPerStudentReportAsync(startDate, endDate)).ToList();

        result.Should().BeEquivalentTo(expected);
        repo.Verify(r => r.GetPerStudentReportAsync(startDate, endDate), Times.Once);
    }

    [Fact(DisplayName = "Scenario 5 - Boundary dates are forwarded exactly for per-algorithm aggregation")]
    public async Task GetPerAlgorithmReportAsync_BoundaryDates_ForwardsExactRange()
    {
        var repo = new Mock<IReportRepository>();
        var startDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc).AddTicks(9999999);
        var expected = new[]
        {
            new PerAlgorithmReportDto
            {
                AlgorithmType = "Sorting",
                AttemptCount = 2,
                AverageScore = 75,
                PassRate = 50
            }
        };

        repo.Setup(r => r.GetPerAlgorithmReportAsync(startDate, endDate)).ReturnsAsync(expected);

        var result = (await BuildSut(repo).GetPerAlgorithmReportAsync(startDate, endDate)).ToList();

        result.Should().BeEquivalentTo(expected);
        repo.Verify(r => r.GetPerAlgorithmReportAsync(startDate, endDate), Times.Once);
    }

    [Fact(DisplayName = "Scenario 6 - Boundary dates are forwarded exactly for per-quiz aggregation")]
    public async Task GetPerQuizReportAsync_BoundaryDates_ForwardsExactRange()
    {
        var repo = new Mock<IReportRepository>();
        var startDate = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc).AddTicks(9999999);
        var expected = new[]
        {
            new PerQuizReportDto
            {
                Title = "Boundary Quiz",
                AttemptCount = 2,
                AverageScore = 75,
                HighestScore = 80,
                LowestScore = 70
            }
        };

        repo.Setup(r => r.GetPerQuizReportAsync(startDate, endDate)).ReturnsAsync(expected);

        var result = (await BuildSut(repo).GetPerQuizReportAsync(startDate, endDate)).ToList();

        result.Should().BeEquivalentTo(expected);
        repo.Verify(r => r.GetPerQuizReportAsync(startDate, endDate), Times.Once);
    }
}
