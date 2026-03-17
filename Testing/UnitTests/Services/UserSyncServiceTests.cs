using System.Security.Claims;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserSyncService"/>.
///
/// Scenarios covered
/// -----------------
/// 1. New-user sync (happy path)   — valid claims, user absent  → creates record, IsNewUser = true
/// 2. Existing-user sync           — user already in DB         → returns existing record, IsNewUser = false
/// 3. Missing Clerk user ID        — 'sub' claim absent          → ArgumentException
/// 4. Database failure handling    — repository throws           → exception propagates correctly
/// </summary>
public class UserSyncServiceTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Builds a <see cref="ClaimsPrincipal"/> with the supplied claim values.</summary>
    private static ClaimsPrincipal BuildPrincipal(
        string? sub      = "clerk_test_001",
        string? email    = "testuser@example.com",
        string? username = "testuser")
    {
        var claims = new List<Claim>();

        if (sub is not null)
            claims.Add(new Claim("sub", sub));

        if (email is not null)
            claims.Add(new Claim(ClaimTypes.Email, email));

        if (username is not null)
            claims.Add(new Claim("username", username));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    /// <summary>Returns a fully-populated <see cref="User"/> for use in mock setups.</summary>
    private static User SampleUser(string clerkUserId = "clerk_test_001") => new()
    {
        UserId      = 42,
        ClerkUserId = clerkUserId,
        Email       = "testuser@example.com",
        Username    = "testuser",
        Role        = "Student",
        XpTotal     = 0,
        IsActive    = true,
        CreatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    // -----------------------------------------------------------------------
    // Scenario 1 — New User Sync (Happy Path)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — New user: should create record and return IsNewUser = true")]
    public async Task SyncUserFromClaimsAsync_NewUser_CreatesRecordAndReturnsIsNewUserTrue()
    {
        // Arrange
        var repoMock = new Mock<IUserRepository>();

        // No existing user found
        repoMock
            .Setup(r => r.GetByClerkUserIdAsync("clerk_test_001"))
            .ReturnsAsync((User?)null);

        // Return the saved user after creation
        repoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(SampleUser());

        var sut = new UserSyncService(repoMock.Object, NullLogger<UserSyncService>.Instance);
        var principal = BuildPrincipal();

        // Act
        var (dto, isNewUser) = await sut.SyncUserFromClaimsAsync(principal);

        // Assert
        isNewUser.Should().BeTrue("a new record should have been provisioned");

        dto.ClerkUserId.Should().Be("clerk_test_001");
        dto.Email.Should().Be("testuser@example.com");
        dto.Username.Should().Be("testuser");
        dto.Role.Should().Be("Student");
        dto.UserId.Should().Be(42);

        // Verify the repository was asked to create exactly one user
        repoMock.Verify(r => r.CreateAsync(It.Is<User>(u =>
            u.ClerkUserId == "clerk_test_001" &&
            u.Email       == "testuser@example.com" &&
            u.Username    == "testuser" &&
            u.Role        == "Student")),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // Scenario 2 — Existing User Sync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 2 — Existing user: should return existing record and IsNewUser = false")]
    public async Task SyncUserFromClaimsAsync_ExistingUser_ReturnsExistingRecordAndIsNewUserFalse()
    {
        // Arrange
        var repoMock = new Mock<IUserRepository>();

        // Existing user is found in the DB
        repoMock
            .Setup(r => r.GetByClerkUserIdAsync("clerk_test_001"))
            .ReturnsAsync(SampleUser());

        var sut = new UserSyncService(repoMock.Object, NullLogger<UserSyncService>.Instance);
        var principal = BuildPrincipal();

        // Act
        var (dto, isNewUser) = await sut.SyncUserFromClaimsAsync(principal);

        // Assert
        isNewUser.Should().BeFalse("no new record should be created for an existing user");

        dto.ClerkUserId.Should().Be("clerk_test_001");
        dto.Email.Should().Be("testuser@example.com");
        dto.Username.Should().Be("testuser");
        dto.UserId.Should().Be(42);

        // CreateAsync must never be called when the user already exists
        repoMock.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Scenario 3 — Missing Clerk User ID (sub claim absent)
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — Missing sub claim: should throw ArgumentException")]
    public async Task SyncUserFromClaimsAsync_MissingSubClaim_ThrowsArgumentException()
    {
        // Arrange
        var repoMock = new Mock<IUserRepository>();

        var sut = new UserSyncService(repoMock.Object, NullLogger<UserSyncService>.Instance);

        // Build a principal that intentionally omits the 'sub' claim
        var principal = BuildPrincipal(sub: null);

        // Act
        Func<Task> act = async () => await sut.SyncUserFromClaimsAsync(principal);

        // Assert
        await act.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*'sub' claim*",
                because: "the service must reject JWTs without a Clerk user ID");

        // Repository must not be touched when claims are invalid
        repoMock.Verify(r => r.GetByClerkUserIdAsync(It.IsAny<string>()), Times.Never);
        repoMock.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Scenario 4 — Database Failure Handling
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 4 — DB exception: should propagate the exception to the caller")]
    public async Task SyncUserFromClaimsAsync_DatabaseThrows_ExceptionPropagates()
    {
        // Arrange
        var repoMock = new Mock<IUserRepository>();

        // Simulate a transient database failure
        repoMock
            .Setup(r => r.GetByClerkUserIdAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Simulated DB failure: connection lost."));

        var sut = new UserSyncService(repoMock.Object, NullLogger<UserSyncService>.Instance);
        var principal = BuildPrincipal();

        // Act
        Func<Task> act = async () => await sut.SyncUserFromClaimsAsync(principal);

        // Assert
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Simulated DB failure*",
                because: "infrastructure exceptions must not be swallowed by the service layer");

        // CreateAsync must not be called if GetByClerkUserIdAsync already threw
        repoMock.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }
}
