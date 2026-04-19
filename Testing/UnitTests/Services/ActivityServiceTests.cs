using backend.DTOs;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

public class ActivityServiceTests
{
    [Fact(DisplayName = "GetRecentActivityAsync delegates to the quiz attempt repository")]
    public async Task GetRecentActivityAsync_DelegatesToRepository()
    {
        var repository = new Mock<IQuizAttemptRepository>();
        repository
            .Setup(r => r.GetRecentActivityAsync(5, 20))
            .ReturnsAsync(new[]
            {
                new ActivityItemDto
                {
                    Type = "quiz",
                    Title = "Binary Search Quiz",
                    XpEarned = 20,
                    CreatedAt = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc)
                }
            });

        var result = (await new ActivityService(repository.Object).GetRecentActivityAsync(5, 20)).ToList();

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Binary Search Quiz");
    }
}
