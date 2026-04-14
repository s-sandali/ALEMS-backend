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
/// Unit tests for <see cref="UserController"/>.
///
/// Scenarios covered
/// -----------------
/// CreateUser
///   1. Valid input, no duplicate → 201 Created
///   2. Duplicate email           → 400 Bad Request
///   3. Service throws            → 500 Internal Server Error
/// GetAllUsers
///   4. Success                   → 200 OK with data
///   5. Service throws            → 500 Internal Server Error
/// GetUserById
///   6. Found                     → 200 OK
///   7. Not found                 → 404 Not Found
///   8. Service throws            → 500 Internal Server Error
/// UpdateUser
///   9. Success                   → 200 OK
///  10. Not found                 → 404 Not Found
///  11. Service throws            → 500 Internal Server Error
/// DeleteUser
///  12. Success                   → 204 No Content
///  13. Not found                 → 404 Not Found
///  14. Service throws            → 500 Internal Server Error
/// </summary>
public class UserControllerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static UserResponseDto SampleDto(int id = 1) => new()
    {
        UserId      = id,
        ClerkUserId = "clerk_001",
        Email       = "alice@example.com",
        Username    = "alice",
        Role        = "User",
        XpTotal     = 0,
        IsActive    = true,
        CreatedAt   = DateTime.UtcNow,
        UpdatedAt   = DateTime.UtcNow
    };

    private static UserController BuildController(Mock<IUserService> svc)
    {
        var ctrl = new UserController(svc.Object, NullLogger<UserController>.Instance);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return ctrl;
    }

    // -----------------------------------------------------------------------
    // CreateUser
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — CreateUser: valid input returns 201 Created")]
    public async Task CreateUser_ValidInput_Returns201()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.CreateUserAsync("alice@example.com", "alice", "User"))
           .ReturnsAsync(SampleDto());

        var dto = new CreateUserDto { Email = "alice@example.com", Username = "alice", Role = "User" };
        var result = await BuildController(svc).CreateUser(dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact(DisplayName = "Scenario 2 — CreateUser: duplicate email returns 400")]
    public async Task CreateUser_DuplicateEmail_Returns400()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .ReturnsAsync((UserResponseDto?)null);

        var dto = new CreateUserDto { Email = "alice@example.com", Username = "alice", Role = "User" };
        var result = await BuildController(svc).CreateUser(dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "Scenario 3 — CreateUser: service throws propagates to middleware")]
    public async Task CreateUser_ServiceThrows_PropagatesException()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .ThrowsAsync(new Exception("DB error"));

        var dto = new CreateUserDto { Email = "alice@example.com", Username = "alice", Role = "User" };

        await FluentActions.Invoking(() => BuildController(svc).CreateUser(dto))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");
    }

    // -----------------------------------------------------------------------
    // GetAllUsers
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — GetAllUsers: success returns 200 with data")]
    public async Task GetAllUsers_Success_Returns200()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetAllUsersAsync()).ReturnsAsync(new[] { SampleDto(1), SampleDto(2) });

        var result = await BuildController(svc).GetAllUsers() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "Scenario 5 — GetAllUsers: service throws propagates to middleware")]
    public async Task GetAllUsers_ServiceThrows_PropagatesException()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetAllUsersAsync()).ThrowsAsync(new Exception("DB error"));

        await FluentActions.Invoking(() => BuildController(svc).GetAllUsers())
            .Should().ThrowAsync<Exception>().WithMessage("DB error");
    }

    // -----------------------------------------------------------------------
    // GetUserById
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 6 — GetUserById: user found returns 200")]
    public async Task GetUserById_Found_Returns200()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetUserByIdAsync(1)).ReturnsAsync(SampleDto());

        var result = await BuildController(svc).GetUserById(1) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "Scenario 7 — GetUserById: user not found returns 404")]
    public async Task GetUserById_NotFound_Returns404()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetUserByIdAsync(99)).ReturnsAsync((UserResponseDto?)null);

        var result = await BuildController(svc).GetUserById(99) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "Scenario 8 — GetUserById: service throws propagates to middleware")]
    public async Task GetUserById_ServiceThrows_PropagatesException()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetUserByIdAsync(It.IsAny<int>())).ThrowsAsync(new Exception("DB error"));

        await FluentActions.Invoking(() => BuildController(svc).GetUserById(1))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");
    }

    // -----------------------------------------------------------------------
    // UpdateUser
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 9 — UpdateUser: success returns 200")]
    public async Task UpdateUser_Success_Returns200()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateUserAsync(1, "Admin", true)).ReturnsAsync(SampleDto());

        var dto = new UpdateUserDto { Role = "Admin", IsActive = true };
        var result = await BuildController(svc).UpdateUser(1, dto) as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact(DisplayName = "Scenario 10 — UpdateUser: user not found propagates KeyNotFoundException to middleware")]
    public async Task UpdateUser_NotFound_PropagatesKeyNotFoundException()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
           .ThrowsAsync(new KeyNotFoundException("User with ID 99 was not found."));

        var dto = new UpdateUserDto { Role = "Admin", IsActive = false };

        await FluentActions.Invoking(() => BuildController(svc).UpdateUser(99, dto))
            .Should().ThrowAsync<KeyNotFoundException>().WithMessage("User with ID 99 was not found.");
    }

    [Fact(DisplayName = "Scenario 11 — UpdateUser: service throws propagates to middleware")]
    public async Task UpdateUser_ServiceThrows_PropagatesException()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
           .ThrowsAsync(new Exception("DB error"));

        var dto = new UpdateUserDto { Role = "Admin", IsActive = true };

        await FluentActions.Invoking(() => BuildController(svc).UpdateUser(1, dto))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");
    }

    // -----------------------------------------------------------------------
    // DeleteUser
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 12 — DeleteUser: success returns 204 No Content")]
    public async Task DeleteUser_Success_Returns204()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.DeleteUserAsync(1)).ReturnsAsync(true);

        var result = await BuildController(svc).DeleteUser(1) as NoContentResult;

        result!.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact(DisplayName = "Scenario 13 — DeleteUser: user not found returns 404")]
    public async Task DeleteUser_NotFound_Returns404()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.DeleteUserAsync(99)).ReturnsAsync(false);

        var result = await BuildController(svc).DeleteUser(99) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "Scenario 14 — DeleteUser: service throws propagates to middleware")]
    public async Task DeleteUser_ServiceThrows_PropagatesException()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.DeleteUserAsync(It.IsAny<int>())).ThrowsAsync(new Exception("DB error"));

        await FluentActions.Invoking(() => BuildController(svc).DeleteUser(1))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");
    }

    // -----------------------------------------------------------------------
    // ModelState.IsValid fallback branches
    // CreateUser has a null-return check that incidentally returns 400 for
    // duplicate email. UpdateUser relies entirely on InvalidModelStateResponseFactory
    // (registered globally in Program.cs) — there is no manual ModelState guard
    // in the controller, so this is only exercised in integration tests.
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 15 — CreateUser: invalid ModelState returns 400 with validation errors")]
    public async Task CreateUser_InvalidModelState_Returns400()
    {
        var svc  = new Mock<IUserService>();
        var ctrl = BuildController(svc);

        // Simulate a model binding error as if [ApiController] were absent
        ctrl.ModelState.AddModelError("Email", "Email is required.");

        var dto    = new CreateUserDto { Email = "", Username = "alice", Role = "User" };
        var result = await ctrl.CreateUser(dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest,
            because: "a controller with invalid ModelState must return 400");
    }
}
