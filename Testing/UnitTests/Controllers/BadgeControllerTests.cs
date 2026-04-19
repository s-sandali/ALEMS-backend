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

public class BadgeControllerTests
{
    private static BadgeResponseDto CreateBadgeDto(int id, string name) => new()
    {
        BadgeId = id,
        BadgeName = name,
        BadgeDescription = $"{name} description",
        XpThreshold = 100,
        IconType = "star",
        IconColor = "#123456",
        UnlockHint = "Keep going"
    };

    private static BadgeController BuildController(Mock<IBadgeService> service)
    {
        return new BadgeController(service.Object, NullLogger<BadgeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact(DisplayName = "GetAllBadges returns 200 OK with badge data")]
    public async Task GetAllBadges_Success_ReturnsOk()
    {
        var service = new Mock<IBadgeService>();
        service
            .Setup(s => s.GetAllBadgesAsync())
            .ReturnsAsync(new[] { CreateBadgeDto(1, "Bronze"), CreateBadgeDto(2, "Silver") });

        var result = await BuildController(service).GetAllBadges() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value.Should().BeAssignableTo<IEnumerable<BadgeResponseDto>>();
    }

    [Fact(DisplayName = "GetBadgeById returns 404 when the badge does not exist")]
    public async Task GetBadgeById_Missing_ReturnsNotFound()
    {
        var service = new Mock<IBadgeService>();
        service
            .Setup(s => s.GetBadgeByIdAsync(99))
            .ReturnsAsync((BadgeResponseDto?)null);

        var result = await BuildController(service).GetBadgeById(99) as NotFoundObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "GetUserEarnedBadges returns 200 OK with earned badges")]
    public async Task GetUserEarnedBadges_Success_ReturnsOk()
    {
        var service = new Mock<IBadgeService>();
        service
            .Setup(s => s.GetUserEarnedBadgesAsync(5))
            .ReturnsAsync(new[] { CreateBadgeDto(1, "Bronze") });

        var result = await BuildController(service).GetUserEarnedBadges(5) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "AwardUnlockedBadges returns 200 OK with awarded badges payload")]
    public async Task AwardUnlockedBadges_Success_ReturnsOk()
    {
        var service = new Mock<IBadgeService>();
        service
            .Setup(s => s.AwardUnlockedBadgesAsync(5))
            .ReturnsAsync(new[] { CreateBadgeDto(2, "Binary Search") });

        var result = await BuildController(service).AwardUnlockedBadges(5) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value!.GetType().GetProperty("awardedBadges")!.GetValue(result.Value)
            .Should().BeAssignableTo<IEnumerable<BadgeResponseDto>>();
    }

    [Fact(DisplayName = "AwardBadge returns 409 Conflict when the badge cannot be awarded")]
    public async Task AwardBadge_Failure_ReturnsConflict()
    {
        var service = new Mock<IBadgeService>();
        service
            .Setup(s => s.AwardBadgeAsync(5, 3))
            .ReturnsAsync(false);

        var result = await BuildController(service).AwardBadge(5, 3) as ConflictObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact(DisplayName = "GetAllBadges returns 500 when the service throws")]
    public async Task GetAllBadges_ServiceThrows_ReturnsServerError()
    {
        var service = new Mock<IBadgeService>();
        service
            .Setup(s => s.GetAllBadgesAsync())
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var result = await BuildController(service).GetAllBadges() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
