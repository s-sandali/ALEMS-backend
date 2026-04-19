using backend.Controllers;
using backend.DTOs;
using backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Controllers;

public class AdminControllerTests
{
    private static AdminController BuildController(Mock<IAdminService> service)
    {
        return new AdminController(service.Object, NullLogger<AdminController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact(DisplayName = "GetPlatformStats returns 200 OK with platform statistics")]
    public async Task GetPlatformStats_Success_ReturnsOk()
    {
        var service = new Mock<IAdminService>();
        service
            .Setup(s => s.GetPlatformStatsAsync())
            .ReturnsAsync(new AdminStatsDto
            {
                TotalUsers = 10,
                TotalQuizzes = 5,
                TotalAttempts = 40,
                AveragePassRate = 62.5
            });

        var result = await BuildController(service).GetPlatformStats() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeOfType<AdminStatsDto>();
    }

    [Fact(DisplayName = "GetPlatformStats returns 500 when the service throws")]
    public async Task GetPlatformStats_ServiceThrows_ReturnsServerError()
    {
        var service = new Mock<IAdminService>();
        service
            .Setup(s => s.GetPlatformStatsAsync())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await BuildController(service).GetPlatformStats() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact(DisplayName = "GetLeaderboard returns 200 OK with leaderboard entries")]
    public async Task GetLeaderboard_Success_ReturnsOk()
    {
        var service = new Mock<IAdminService>();
        service
            .Setup(s => s.GetLeaderboardAsync())
            .ReturnsAsync(new[]
            {
                new LeaderboardEntryDto { UserId = 1, Username = "alice", Rank = 1, XpTotal = 500 }
            });

        var result = await BuildController(service).GetLeaderboard() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeAssignableTo<IEnumerable<LeaderboardEntryDto>>();
    }

    [Fact(DisplayName = "GetLeaderboard returns 500 when the service throws")]
    public async Task GetLeaderboard_ServiceThrows_ReturnsServerError()
    {
        var service = new Mock<IAdminService>();
        service
            .Setup(s => s.GetLeaderboardAsync())
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await BuildController(service).GetLeaderboard() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
