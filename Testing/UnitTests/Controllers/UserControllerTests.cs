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
        Role        = "Student",
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
        svc.Setup(s => s.CreateUserAsync("alice@example.com", "alice", "Student"))
           .ReturnsAsync(SampleDto());

        var dto = new CreateUserDto { Email = "alice@example.com", Username = "alice", Role = "Student" };
        var result = await BuildController(svc).CreateUser(dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact(DisplayName = "Scenario 2 — CreateUser: duplicate email returns 400")]
    public async Task CreateUser_DuplicateEmail_Returns400()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .ReturnsAsync((UserResponseDto?)null);

        var dto = new CreateUserDto { Email = "alice@example.com", Username = "alice", Role = "Student" };
        var result = await BuildController(svc).CreateUser(dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact(DisplayName = "Scenario 3 — CreateUser: service throws returns 500")]
    public async Task CreateUser_ServiceThrows_Returns500()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .ThrowsAsync(new Exception("DB error"));

        var dto = new CreateUserDto { Email = "alice@example.com", Username = "alice", Role = "Student" };
        var result = await BuildController(svc).CreateUser(dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
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

    [Fact(DisplayName = "Scenario 5 — GetAllUsers: service throws returns 500")]
    public async Task GetAllUsers_ServiceThrows_Returns500()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetAllUsersAsync()).ThrowsAsync(new Exception("DB error"));

        var result = await BuildController(svc).GetAllUsers() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
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

    [Fact(DisplayName = "Scenario 8 — GetUserById: service throws returns 500")]
    public async Task GetUserById_ServiceThrows_Returns500()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.GetUserByIdAsync(It.IsAny<int>())).ThrowsAsync(new Exception("DB error"));

        var result = await BuildController(svc).GetUserById(1) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
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

    [Fact(DisplayName = "Scenario 10 — UpdateUser: user not found returns 404")]
    public async Task UpdateUser_NotFound_Returns404()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
           .ThrowsAsync(new KeyNotFoundException("User with ID 99 was not found."));

        var dto = new UpdateUserDto { Role = "Admin", IsActive = false };
        var result = await BuildController(svc).UpdateUser(99, dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact(DisplayName = "Scenario 11 — UpdateUser: service throws returns 500")]
    public async Task UpdateUser_ServiceThrows_Returns500()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.UpdateUserAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>()))
           .ThrowsAsync(new Exception("DB error"));

        var dto = new UpdateUserDto { Role = "Admin", IsActive = true };
        var result = await BuildController(svc).UpdateUser(1, dto) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
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

    [Fact(DisplayName = "Scenario 14 — DeleteUser: service throws returns 500")]
    public async Task DeleteUser_ServiceThrows_Returns500()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.DeleteUserAsync(It.IsAny<int>())).ThrowsAsync(new Exception("DB error"));

        var result = await BuildController(svc).DeleteUser(1) as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
