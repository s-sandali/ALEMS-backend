using System.Security.Claims;
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

/// <summary>
/// Unit tests for <see cref="QuizController"/>.
///
/// Scenarios covered
/// -----------------
/// GetAllQuizzes
///   1. Returns 200 OK with all quizzes
/// GetQuizById
///   2. Returns 200 OK when quiz found
///   3. Returns 404 Not Found when quiz missing
/// CreateQuiz
///   4. Returns 201 Created with data on success
///   5. Returns 401 Unauthorized when JWT has no user identifier
///   6. Returns 400 Bad Request when service throws ArgumentException (algorithm missing)
///   7. Returns 404 Not Found when service throws KeyNotFoundException (user not synced)
/// UpdateQuiz
///   8. Returns 200 OK with updated data on success
///   9. Returns 404 Not Found when service throws KeyNotFoundException
/// DeleteQuiz
///  10. Returns 204 No Content on successful soft-delete
///  11. Returns 404 Not Found when quiz not found
/// </summary>
public class QuizControllerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static QuizResponseDto SampleDto(int id = 1) => new()
    {
        QuizId        = id,
        AlgorithmId   = 10,
        CreatedBy     = 5,
        Title         = "Sample Quiz",
        PassScore     = 70,
        IsActive      = true,
        CreatedAt     = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt     = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static QuizController BuildController(
        Mock<IQuizService> svc,
        string? clerkUserId = "clerk_001")
    {
        var ctrl = new QuizController(svc.Object, NullLogger<QuizController>.Instance);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = clerkUserId is null
                    ? new ClaimsPrincipal(new ClaimsIdentity())  // no claims
                    : new ClaimsPrincipal(new ClaimsIdentity(new[]
                      {
                          new Claim(ClaimTypes.NameIdentifier, clerkUserId)
                      }, "TestAuth"))
            }
        };
        return ctrl;
    }

    // -----------------------------------------------------------------------
    // GetAllQuizzes
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — GetAllQuizzes: returns 200 OK with all quizzes")]
    public async Task Should_Return200_When_GetAllQuizzesCalled()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.GetAllQuizzesAsync())
           .ReturnsAsync(new[] { SampleDto(1), SampleDto(2) });

        // Act
        var result = await BuildController(svc).GetAllQuizzes() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);

        var body = result.Value!.GetType().GetProperty("data")!.GetValue(result.Value)
                   as IEnumerable<QuizResponseDto>;
        body!.Should().HaveCount(2);
    }

    // -----------------------------------------------------------------------
    // GetQuizById
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — GetQuizById: returns 200 OK when quiz found")]
    public async Task Should_Return200_When_QuizFound()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.GetQuizByIdAsync(1)).ReturnsAsync(SampleDto(1));

        // Act
        var result = await BuildController(svc).GetQuizById(1) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);
    }

    [Fact(DisplayName = "Scenario 3 — GetQuizById: returns 404 Not Found when quiz missing")]
    public async Task Should_Return404_When_QuizNotFound()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.GetQuizByIdAsync(99)).ReturnsAsync((QuizResponseDto?)null);

        // Act
        var result = await BuildController(svc).GetQuizById(99) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // CreateQuiz
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — CreateQuiz: returns 201 Created on success")]
    public async Task Should_Return201_When_QuizCreatedSuccessfully()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.CreateQuizAsync(It.IsAny<CreateQuizDto>(), "clerk_001"))
           .ReturnsAsync(SampleDto(1));

        var dto    = new CreateQuizDto { AlgorithmId = 10, Title = "New Quiz" };
        var result = await BuildController(svc).CreateQuiz(dto) as ObjectResult;

        // Assert
        result!.StatusCode.Should().Be(201);

        var status = result.Value!.GetType().GetProperty("status")!.GetValue(result.Value) as string;
        status.Should().Be("success");
    }

    [Fact(DisplayName = "Scenario 5 — CreateQuiz: returns 401 when JWT has no user identifier")]
    public async Task Should_Return401_When_JwtMissingUserIdentifier()
    {
        // Arrange — controller built with no claims
        var svc    = new Mock<IQuizService>();
        var result = await BuildController(svc, clerkUserId: null)
                         .CreateQuiz(new CreateQuizDto { AlgorithmId = 10, Title = "Q" }) as UnauthorizedObjectResult;

        // Assert
        result!.StatusCode.Should().Be(401);
    }

    [Fact(DisplayName = "Scenario 6 — CreateQuiz: returns 400 Bad Request when algorithm not found")]
    public async Task Should_Return400_When_AlgorithmNotFound()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.CreateQuizAsync(It.IsAny<CreateQuizDto>(), "clerk_001"))
           .ThrowsAsync(new ArgumentException("Algorithm with ID 99 does not exist."));

        var result = await BuildController(svc)
                         .CreateQuiz(new CreateQuizDto { AlgorithmId = 99, Title = "Q" }) as BadRequestObjectResult;

        // Assert
        result!.StatusCode.Should().Be(400);

        var message = result.Value!.GetType().GetProperty("message")!.GetValue(result.Value) as string;
        message.Should().Contain("99");
    }

    [Fact(DisplayName = "Scenario 7 — CreateQuiz: returns 404 when user not synced")]
    public async Task Should_Return404_When_UserNotSynced()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.CreateQuizAsync(It.IsAny<CreateQuizDto>(), "clerk_001"))
           .ThrowsAsync(new KeyNotFoundException("Authenticated user does not have a local account."));

        var result = await BuildController(svc)
                         .CreateQuiz(new CreateQuizDto { AlgorithmId = 1, Title = "Q" }) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // UpdateQuiz
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 8 — UpdateQuiz: returns 200 OK with updated data")]
    public async Task Should_Return200_When_UpdateSucceeds()
    {
        // Arrange
        var updated = SampleDto(1);
        updated.Title = "Updated Title";

        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.UpdateQuizAsync(1, It.IsAny<UpdateQuizDto>())).ReturnsAsync(updated);

        var dto    = new UpdateQuizDto { Title = "Updated Title", IsActive = true };
        var result = await BuildController(svc).UpdateQuiz(1, dto) as OkObjectResult;

        // Assert
        result!.StatusCode.Should().Be(200);

        var status = result.Value!.GetType().GetProperty("status")!.GetValue(result.Value) as string;
        status.Should().Be("success");
    }

    [Fact(DisplayName = "Scenario 9 — UpdateQuiz: returns 404 Not Found when quiz not found")]
    public async Task Should_Return404_When_UpdateTargetNotFound()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.UpdateQuizAsync(99, It.IsAny<UpdateQuizDto>()))
           .ThrowsAsync(new KeyNotFoundException("Quiz with ID 99 was not found."));

        var result = await BuildController(svc)
                         .UpdateQuiz(99, new UpdateQuizDto { Title = "T", IsActive = true }) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------------
    // DeleteQuiz
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 10 — DeleteQuiz: returns 204 No Content on successful soft-delete")]
    public async Task Should_Return204_When_DeleteSucceeds()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.DeleteQuizAsync(1)).ReturnsAsync(true);

        var result = await BuildController(svc).DeleteQuiz(1) as NoContentResult;

        // Assert
        result!.StatusCode.Should().Be(204);
    }

    [Fact(DisplayName = "Scenario 11 — DeleteQuiz: returns 404 Not Found when quiz not found")]
    public async Task Should_Return404_When_DeleteTargetNotFound()
    {
        // Arrange
        var svc = new Mock<IQuizService>();
        svc.Setup(s => s.DeleteQuizAsync(99)).ReturnsAsync(false);

        var result = await BuildController(svc).DeleteQuiz(99) as NotFoundObjectResult;

        // Assert
        result!.StatusCode.Should().Be(404);
    }
}
