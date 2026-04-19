using backend.DTOs;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class ActivityHeatmapServiceTests
{
    [Fact(DisplayName = "GetDailyActivityAsync delegates to the quiz attempt repository")]
    public async Task GetDailyActivityAsync_DelegatesToRepository()
    {
        var repository = new Mock<IQuizAttemptRepository>();
        repository
            .Setup(r => r.GetDailyActivityAsync(5))
            .ReturnsAsync(new[]
            {
                new ActivityHeatmapDto
                {
                    Date = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                    Count = 3
                }
            });

        var result = (await new ActivityHeatmapService(repository.Object).GetDailyActivityAsync(5)).ToList();

        result.Should().HaveCount(1);
        result[0].Count.Should().Be(3);
    }
}
