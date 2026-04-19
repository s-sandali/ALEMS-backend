using System.Security.Claims;
using backend.Controllers;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Controllers;

public class LeaderboardControllerTests
{
    private static LeaderboardController BuildController(
        Mock<ILeaderboardService> leaderboardService,
        Mock<IUserRepository> userRepository,
        string? clerkUserId = "clerk_001")
    {
        var controller = new LeaderboardController(
            leaderboardService.Object,
            userRepository.Object,
            NullLogger<LeaderboardController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = clerkUserId is null
                    ? new ClaimsPrincipal(new ClaimsIdentity())
                    : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", clerkUserId)], "TestAuth"))
            }
        };

        return controller;
    }

    [Fact(DisplayName = "GetLeaderboard returns 401 when the user claim is missing")]
    public async Task GetLeaderboard_MissingClaim_ReturnsUnauthorized()
    {
        var leaderboardService = new Mock<ILeaderboardService>();
        var userRepository = new Mock<IUserRepository>();

        var result = await BuildController(leaderboardService, userRepository, clerkUserId: null).GetLeaderboard() as UnauthorizedObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        userRepository.Verify(r => r.GetByClerkUserIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName = "GetLeaderboard returns 404 when no local user exists for the Clerk identity")]
    public async Task GetLeaderboard_UnknownUser_ReturnsNotFound()
    {
        var leaderboardService = new Mock<ILeaderboardService>();
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync((User?)null);

        var result = await BuildController(leaderboardService, userRepository).GetLeaderboard() as NotFoundObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "GetLeaderboard returns 200 OK with leaderboard data for the current user")]
    public async Task GetLeaderboard_Success_ReturnsOk()
    {
        var leaderboardService = new Mock<ILeaderboardService>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync(new User { UserId = 7, ClerkUserId = "clerk_001", Username = "alice" });
        leaderboardService
            .Setup(s => s.GetLeaderboardAsync(7, 10))
            .ReturnsAsync(new[]
            {
                new LeaderboardEntryDto { UserId = 7, Username = "alice", Rank = 3, XpTotal = 250, IsCurrentUser = true }
            });

        var result = await BuildController(leaderboardService, userRepository).GetLeaderboard() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value!.GetType().GetProperty("status")!.GetValue(result.Value).Should().Be("success");
    }

    [Fact(DisplayName = "GetLeaderboard returns 500 when the service throws unexpectedly")]
    public async Task GetLeaderboard_ServiceThrows_ReturnsServerError()
    {
        var leaderboardService = new Mock<ILeaderboardService>();
        var userRepository = new Mock<IUserRepository>();

        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync(new User { UserId = 7, ClerkUserId = "clerk_001", Username = "alice" });
        leaderboardService
            .Setup(s => s.GetLeaderboardAsync(7, 10))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await BuildController(leaderboardService, userRepository).GetLeaderboard() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
