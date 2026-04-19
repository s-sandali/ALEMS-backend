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

public class AlgorithmControllerTests
{
    private static AlgorithmResponseDto CreateDto(int id, string name) => new()
    {
        AlgorithmId = id,
        Name = name,
        Category = "Sorting",
        Description = $"{name} description",
        TimeComplexityBest = "O(n)",
        TimeComplexityAverage = "O(n log n)",
        TimeComplexityWorst = "O(n^2)",
        QuizAvailable = true,
        CreatedAt = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private static AlgorithmController BuildController(Mock<IAlgorithmService> service)
    {
        return new AlgorithmController(service.Object, NullLogger<AlgorithmController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact(DisplayName = "GetAllAlgorithms returns 200 OK with success wrapper")]
    public async Task GetAllAlgorithms_Success_ReturnsOk()
    {
        var service = new Mock<IAlgorithmService>();
        service
            .Setup(s => s.GetAllAlgorithmsAsync())
            .ReturnsAsync(new[] { CreateDto(1, "Bubble Sort"), CreateDto(2, "Heap Sort") });

        var result = await BuildController(service).GetAllAlgorithms() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Value!.GetType().GetProperty("status")!.GetValue(result.Value).Should().Be("success");
    }

    [Fact(DisplayName = "GetAlgorithmById returns 200 OK when the algorithm exists")]
    public async Task GetAlgorithmById_Found_ReturnsOk()
    {
        var service = new Mock<IAlgorithmService>();
        service
            .Setup(s => s.GetAlgorithmByIdAsync(4))
            .ReturnsAsync(CreateDto(4, "Merge Sort"));

        var result = await BuildController(service).GetAlgorithmById(4) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "GetAlgorithmById returns 404 when the algorithm does not exist")]
    public async Task GetAlgorithmById_Missing_ReturnsNotFound()
    {
        var service = new Mock<IAlgorithmService>();
        service
            .Setup(s => s.GetAlgorithmByIdAsync(99))
            .ReturnsAsync((AlgorithmResponseDto?)null);

        var result = await BuildController(service).GetAlgorithmById(99) as NotFoundObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}
