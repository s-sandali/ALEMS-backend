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

public class StudentControllerTests
{
    private static StudentController BuildController(
        Mock<IUserService>? userService = null,
        Mock<IUserRepository>? userRepository = null,
        Mock<ILevelingService>? levelingService = null,
        Mock<IQuizAttemptService>? attemptService = null,
        Mock<IActivityService>? activityService = null,
        Mock<IStudentDashboardService>? dashboardService = null,
        Mock<IActivityHeatmapService>? heatmapService = null,
        string? clerkUserId = "clerk_001",
        string? role = null)
    {
        var identity = clerkUserId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity(
            [
                new Claim("sub", clerkUserId),
                new Claim(ClaimTypes.NameIdentifier, clerkUserId)
            ], "TestAuth");

        if (!string.IsNullOrWhiteSpace(role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        var controller = new StudentController(
            (userService ?? new Mock<IUserService>()).Object,
            (userRepository ?? new Mock<IUserRepository>()).Object,
            (levelingService ?? new Mock<ILevelingService>()).Object,
            (attemptService ?? new Mock<IQuizAttemptService>()).Object,
            (activityService ?? new Mock<IActivityService>()).Object,
            (dashboardService ?? new Mock<IStudentDashboardService>()).Object,
            (heatmapService ?? new Mock<IActivityHeatmapService>()).Object,
            NullLogger<StudentController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return controller;
    }

    [Fact(DisplayName = "GetStudentDashboard returns 401 when the authenticated user claim is missing")]
    public async Task GetStudentDashboard_MissingClaim_ReturnsUnauthorized()
    {
        var userRepository = new Mock<IUserRepository>();

        var result = await BuildController(userRepository: userRepository, clerkUserId: null).GetStudentDashboard(5) as UnauthorizedObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact(DisplayName = "GetUserProgression returns 200 OK with derived progression data for an authorized student")]
    public async Task GetUserProgression_AuthorizedStudent_ReturnsOk()
    {
        var userService = new Mock<IUserService>();
        var userRepository = new Mock<IUserRepository>();
        var levelingService = new Mock<ILevelingService>();

        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync(new User { UserId = 5, ClerkUserId = "clerk_001", Username = "alice" });
        userService
            .Setup(s => s.GetUserByIdAsync(5))
            .ReturnsAsync(new UserResponseDto { UserId = 5, XpTotal = 250, Username = "alice" });
        levelingService.Setup(s => s.CalculateLevel(250)).Returns(2);
        levelingService.Setup(s => s.GetXpForPreviousLevel(2)).Returns(100);
        levelingService.Setup(s => s.GetXpForNextLevel(2)).Returns(382);

        var result = await BuildController(userService, userRepository, levelingService).GetUserProgression(5) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);

        var data = result.Value!.GetType().GetProperty("data")!.GetValue(result.Value) as UserProgressionDto;
        data.Should().NotBeNull();
        data!.CurrentLevel.Should().Be(2);
        data.XpInCurrentLevel.Should().Be(150);
        data.XpNeededForLevel.Should().Be(282);
        data.ProgressPercentage.Should().BeApproximately(53.19, 0.01);
    }

    [Fact(DisplayName = "GetActivityHeatmap returns 403 when a student requests another student's data")]
    public async Task GetActivityHeatmap_DifferentStudent_ReturnsForbid()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(r => r.GetByClerkUserIdAsync("clerk_001"))
            .ReturnsAsync(new User { UserId = 5, ClerkUserId = "clerk_001" });

        var result = await BuildController(userRepository: userRepository).GetActivityHeatmap(9);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact(DisplayName = "GetRecentActivity returns 400 when the limit is outside the allowed range")]
    public async Task GetRecentActivity_InvalidLimit_ReturnsBadRequest()
    {
        var result = await BuildController().GetRecentActivity(5, limit: 0) as BadRequestObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "GetStudentAttemptHistory allows admin users to read any student's attempts")]
    public async Task GetStudentAttemptHistory_AdminUser_ReturnsOk()
    {
        var userService = new Mock<IUserService>();
        var attemptService = new Mock<IQuizAttemptService>();

        userService
            .Setup(s => s.GetUserByIdAsync(9))
            .ReturnsAsync(new UserResponseDto { UserId = 9, Username = "student" });
        attemptService
            .Setup(s => s.GetUserAttemptHistoryAsync(9, 2, 20))
            .ReturnsAsync(new StudentAttemptHistoryResponseDto
            {
                Attempts = new List<UserAttemptHistoryDto>
                {
                    new()
                    {
                        AttemptId = 12,
                        QuizId = 4,
                        QuizTitle = "Binary Search",
                        AlgorithmName = "Searching",
                        Score = 80
                    }
                },
                Page = 2,
                PageSize = 20,
                TotalAttempts = 1
            });

        var result = await BuildController(
            userService: userService,
            attemptService: attemptService,
            clerkUserId: "admin_clerk",
            role: "Admin").GetStudentAttemptHistory(9, page: 2, pageSize: 20) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value!.GetType().GetProperty("status")!.GetValue(result.Value).Should().Be("success");
    }
}
