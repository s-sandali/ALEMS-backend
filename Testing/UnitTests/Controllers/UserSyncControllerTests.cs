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
/// Unit tests for <see cref="UserSyncController"/>.
///
/// Scenarios covered
/// -----------------
///   1. Missing 'sub' claim       → 401 Unauthorized
///   2. New user (isNewUser=true) → 201 Created
///   3. Existing user             → 200 OK
///   4. Service throws            → 500 Internal Server Error
///   5. Missing email claim uses  → fallback username from email prefix
/// </summary>
public class UserSyncControllerTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static UserResponseDto SampleDto() => new()
    {
        UserId      = 1,
        ClerkUserId = "clerk_001",
        Email       = "alice@example.com",
        Username    = "alice",
        Role        = "Student",
        XpTotal     = 0,
        IsActive    = true,
        CreatedAt   = DateTime.UtcNow,
        UpdatedAt   = DateTime.UtcNow
    };

    private static UserSyncController BuildController(Mock<IUserService> svc, ClaimsPrincipal user)
    {
        var ctrl = new UserSyncController(svc.Object, NullLogger<UserSyncController>.Instance);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return ctrl;
    }

    private static ClaimsPrincipal BuildPrincipal(
        string? sub      = "clerk_001",
        string? email    = "alice@example.com",
        string? username = "alice")
    {
        var claims = new List<Claim>();
        if (sub is not null)      claims.Add(new Claim("sub", sub));
        if (email is not null)    claims.Add(new Claim(ClaimTypes.Email, email));
        if (username is not null) claims.Add(new Claim("name", username));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    // -----------------------------------------------------------------------
    // Scenario 1 — Missing 'sub' claim → 401
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — Missing sub claim returns 401 Unauthorized")]
    public async Task SyncUser_MissingSubClaim_Returns401()
    {
        var svc  = new Mock<IUserService>();
        var ctrl = BuildController(svc, BuildPrincipal(sub: null));

        var result = await ctrl.SyncUser() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        svc.Verify(s => s.SyncUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — New user → 201 Created
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — New user returns 201 Created")]
    public async Task SyncUser_NewUser_Returns201()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.SyncUserAsync("clerk_001", "alice@example.com", "alice"))
           .ReturnsAsync((SampleDto(), true));

        var ctrl   = BuildController(svc, BuildPrincipal());
        var result = await ctrl.SyncUser() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    // -----------------------------------------------------------------------
    // Scenario 3 — Existing user → 200 OK
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — Existing user returns 200 OK")]
    public async Task SyncUser_ExistingUser_Returns200()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.SyncUserAsync("clerk_001", "alice@example.com", "alice"))
           .ReturnsAsync((SampleDto(), false));

        var ctrl   = BuildController(svc, BuildPrincipal());
        var result = await ctrl.SyncUser() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    // -----------------------------------------------------------------------
    // Scenario 4 — Service throws → 500
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — Service throws returns 500")]
    public async Task SyncUser_ServiceThrows_Returns500()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.SyncUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
           .ThrowsAsync(new Exception("DB error"));

        var ctrl   = BuildController(svc, BuildPrincipal());
        var result = await ctrl.SyncUser() as ObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    // -----------------------------------------------------------------------
    // Scenario 5 — No username claim → falls back to email prefix
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 5 — No username claim: uses email prefix as username")]
    public async Task SyncUser_NoUsernameClaim_FallsBackToEmailPrefix()
    {
        var svc = new Mock<IUserService>();
        svc.Setup(s => s.SyncUserAsync("clerk_001", "alice@example.com", "alice"))
           .ReturnsAsync((SampleDto(), false));

        // No "name" claim — should fall back to email prefix "alice"
        var principal = BuildPrincipal(username: null);
        var ctrl      = BuildController(svc, principal);

        var result = await ctrl.SyncUser() as OkObjectResult;

        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        svc.Verify(s => s.SyncUserAsync("clerk_001", "alice@example.com", "alice"), Times.Once);
    }
}
