using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserService"/>.
///
/// Scenarios covered
/// -----------------
/// SyncUserAsync
///   1. Existing user  → returns DTO, IsNewUser = false
///   2. New user       → creates record, IsNewUser = true
/// CreateUserAsync
///   3. Duplicate email → returns null
///   4. Success         → returns created DTO
/// GetAllUsersAsync
///   5. Returns mapped DTOs for all users
/// GetUserByIdAsync
///   6. User found     → returns DTO
///   7. User not found → returns null
/// UpdateUserAsync
///   8. User not found → returns null
///   9. Success        → returns updated DTO
/// DeleteUserAsync
///  10. User not found → returns false
///  11. Success        → returns true
/// </summary>
public class UserServiceTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static User SampleUser(int id = 1) => new()
    {
        UserId      = id,
        ClerkUserId = "clerk_001",
        Email       = "alice@example.com",
        Username    = "alice",
        Role        = "Student",
        XpTotal     = 100,
        IsActive    = true,
        CreatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt   = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static UserService BuildSut(Mock<IUserRepository> repoMock) =>
        new(repoMock.Object, NullLogger<UserService>.Instance);

    // -----------------------------------------------------------------------
    // SyncUserAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 1 — SyncUserAsync: existing user returns DTO and IsNewUser = false")]
    public async Task SyncUserAsync_ExistingUser_ReturnsExistingDtoAndFalse()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync(SampleUser());

        var (dto, isNew) = await BuildSut(repo).SyncUserAsync("clerk_001", "alice@example.com", "alice");

        isNew.Should().BeFalse();
        dto.ClerkUserId.Should().Be("clerk_001");
        dto.Email.Should().Be("alice@example.com");
        repo.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact(DisplayName = "Scenario 2 — SyncUserAsync: new user creates record and returns IsNewUser = true")]
    public async Task SyncUserAsync_NewUser_CreatesRecordAndReturnsTrue()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByClerkUserIdAsync("clerk_001")).ReturnsAsync((User?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync(SampleUser());

        var (dto, isNew) = await BuildSut(repo).SyncUserAsync("clerk_001", "alice@example.com", "alice");

        isNew.Should().BeTrue();
        dto.UserId.Should().Be(1);
        repo.Verify(r => r.CreateAsync(It.Is<User>(u =>
            u.ClerkUserId == "clerk_001" &&
            u.Email == "alice@example.com" &&
            u.Role == "Student")), Times.Once);
    }

    // -----------------------------------------------------------------------
    // CreateUserAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 3 — CreateUserAsync: duplicate email returns null")]
    public async Task CreateUserAsync_DuplicateEmail_ReturnsNull()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("alice@example.com")).ReturnsAsync(SampleUser());

        var result = await BuildSut(repo).CreateUserAsync("alice@example.com", "alice", "Student");

        result.Should().BeNull();
        repo.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact(DisplayName = "Scenario 4 — CreateUserAsync: success returns mapped DTO")]
    public async Task CreateUserAsync_Success_ReturnsMappedDto()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByEmailAsync("alice@example.com")).ReturnsAsync((User?)null);
        repo.Setup(r => r.CreateAsync(It.IsAny<User>())).ReturnsAsync(SampleUser());

        var result = await BuildSut(repo).CreateUserAsync("alice@example.com", "alice", "Student");

        result.Should().NotBeNull();
        result!.Email.Should().Be("alice@example.com");
        result.Role.Should().Be("Student");
    }

    // -----------------------------------------------------------------------
    // GetAllUsersAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 5 — GetAllUsersAsync: returns mapped DTOs for all users")]
    public async Task GetAllUsersAsync_ReturnsAllMappedDtos()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { SampleUser(1), SampleUser(2) });

        var result = (await BuildSut(repo).GetAllUsersAsync()).ToList();

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(1);
        result[1].UserId.Should().Be(2);
    }

    // -----------------------------------------------------------------------
    // GetUserByIdAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 6 — GetUserByIdAsync: user found returns DTO")]
    public async Task GetUserByIdAsync_UserFound_ReturnsDto()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(SampleUser());

        var result = await BuildSut(repo).GetUserByIdAsync(1);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(1);
    }

    [Fact(DisplayName = "Scenario 7 — GetUserByIdAsync: user not found returns null")]
    public async Task GetUserByIdAsync_UserNotFound_ReturnsNull()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((User?)null);

        var result = await BuildSut(repo).GetUserByIdAsync(99);

        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // UpdateUserAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 8 — UpdateUserAsync: user not found throws KeyNotFoundException")]
    public async Task UpdateUserAsync_UserNotFound_ThrowsKeyNotFoundException()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.UpdateAsync(99, "Admin", true)).ReturnsAsync(false);

        Func<Task> act = () => BuildSut(repo).UpdateUserAsync(99, "Admin", true);

        await act.Should()
            .ThrowAsync<KeyNotFoundException>()
            .WithMessage("*99*",
                because: "the service must throw when the user ID does not exist");

        repo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact(DisplayName = "Scenario 9 — UpdateUserAsync: success returns updated DTO")]
    public async Task UpdateUserAsync_Success_ReturnsUpdatedDto()
    {
        var updated = SampleUser();
        updated.Role = "Admin";

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.UpdateAsync(1, "Admin", true)).ReturnsAsync(true);
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(updated);

        var result = await BuildSut(repo).UpdateUserAsync(1, "Admin", true);

        result.Should().NotBeNull();
        result!.Role.Should().Be("Admin");
    }

    // -----------------------------------------------------------------------
    // DeleteUserAsync
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Scenario 10 — DeleteUserAsync: user not found returns false")]
    public async Task DeleteUserAsync_UserNotFound_ReturnsFalse()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.DeleteAsync(99)).ReturnsAsync(false);

        var result = await BuildSut(repo).DeleteUserAsync(99);

        result.Should().BeFalse();
    }

    [Fact(DisplayName = "Scenario 11 — DeleteUserAsync: success returns true")]
    public async Task DeleteUserAsync_Success_ReturnsTrue()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);

        var result = await BuildSut(repo).DeleteUserAsync(1);

        result.Should().BeTrue();
    }
}
