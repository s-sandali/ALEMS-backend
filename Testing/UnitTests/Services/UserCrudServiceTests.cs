using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace backend.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserService"/> CRUD operations.
///
/// User Story : S1-US4
///
/// Scenarios covered
/// -----------------
/// CREATE
///   1. Valid creation          — new e-mail → creates record, returns populated DTO
///   2. Duplicate email fails   — existing e-mail → returns null, no DB write
/// READ
///   3. Get all returns list    — repository returns several users → all mapped to DTOs
///   4. Get by valid ID         — ID exists → returns matching DTO
///   5. Get by invalid ID       — ID absent → returns null
/// UPDATE
///   6. Valid update            — ID exists, repo succeeds → returns refreshed DTO
///   7. Invalid ID throws       — ID absent, repo returns false → KeyNotFoundException
/// DELETE
///   8. Soft delete             — DeleteAsync succeeds → returns true; IsActive = false
/// </summary>
public class UserCrudServiceTests
{
    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    /// <summary>Returns a fully-populated <see cref="User"/> for use in mock setups.</summary>
    private static User SampleUser(int id = 1, string email = "alice@example.com") => new()
    {
        UserId      = id,
        ClerkUserId = string.Empty,
        Email       = email,
        Username    = "alice",
        Role        = "Student",
        XpTotal     = 0,
        IsActive    = true,
        CreatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt   = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>Creates a new mocked repository and a <see cref="UserService"/> wired to it.</summary>
    private static (Mock<IUserRepository> repoMock, UserService sut) BuildSut()
    {
        var repoMock = new Mock<IUserRepository>();
        var sut = new UserService(repoMock.Object, NullLogger<UserService>.Instance);
        return (repoMock, sut);
    }

    // -----------------------------------------------------------------------
    // CREATE — test 1
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Create 1 — Valid creation: returns populated UserResponseDto")]
    public async Task CreateUserAsync_ValidEmail_ReturnsPopulatedDto()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        // No existing user with this e-mail
        repoMock
            .Setup(r => r.GetByEmailAsync("alice@example.com"))
            .ReturnsAsync((User?)null);

        // Simulate the DB returning the newly-inserted record
        repoMock
            .Setup(r => r.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(SampleUser());

        // Act
        var result = await sut.CreateUserAsync("alice@example.com", "alice", "Student");

        // Assert
        result.Should().NotBeNull("a valid e-mail should produce a new user record");

        result!.UserId.Should().Be(1);
        result.Email.Should().Be("alice@example.com");
        result.Username.Should().Be("alice");
        result.Role.Should().Be("Student");
        result.IsActive.Should().BeTrue();

        // Repository must be asked to insert exactly one record matching the input
        repoMock.Verify(r => r.CreateAsync(It.Is<User>(u =>
            u.Email    == "alice@example.com" &&
            u.Username == "alice"             &&
            u.Role     == "Student")),
            Times.Once);
    }

    // -----------------------------------------------------------------------
    // CREATE — test 2
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Create 2 — Duplicate email: returns null and never writes to DB")]
    public async Task CreateUserAsync_DuplicateEmail_ReturnsNull()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        // The e-mail address already belongs to an existing user
        repoMock
            .Setup(r => r.GetByEmailAsync("alice@example.com"))
            .ReturnsAsync(SampleUser());

        // Act
        var result = await sut.CreateUserAsync("alice@example.com", "alice2", "Student");

        // Assert
        result.Should().BeNull("duplicate e-mail addresses must be rejected at the service layer");

        // No insert must ever be attempted
        repoMock.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // READ — test 3
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Read 1 — Get all: returns one DTO per user, all fields mapped correctly")]
    public async Task GetAllUsersAsync_UsersExist_ReturnsMappedDtoList()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        var alice = SampleUser(1, "alice@example.com");
        var bob   = SampleUser(2, "bob@example.com");
        bob.Username = "bob";

        repoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(new[] { alice, bob });

        // Act
        var result = (await sut.GetAllUsersAsync()).ToList();

        // Assert
        result.Should().HaveCount(2, "the service must return one DTO per repository record");

        result[0].UserId.Should().Be(1);
        result[0].Email.Should().Be("alice@example.com");
        result[0].Username.Should().Be("alice");

        result[1].UserId.Should().Be(2);
        result[1].Email.Should().Be("bob@example.com");
        result[1].Username.Should().Be("bob");
    }

    // -----------------------------------------------------------------------
    // READ — test 4
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Read 2 — Get by valid ID: returns the matching UserResponseDto")]
    public async Task GetUserByIdAsync_ValidId_ReturnsUserDto()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        repoMock
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(SampleUser());

        // Act
        var result = await sut.GetUserByIdAsync(1);

        // Assert
        result.Should().NotBeNull("the user exists in the repository");

        result!.UserId.Should().Be(1);
        result.Email.Should().Be("alice@example.com");
        result.Username.Should().Be("alice");
        result.Role.Should().Be("Student");
    }

    // -----------------------------------------------------------------------
    // READ — test 5
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Read 3 — Get by invalid ID: returns null")]
    public async Task GetUserByIdAsync_InvalidId_ReturnsNull()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        repoMock
            .Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((User?)null);

        // Act
        var result = await sut.GetUserByIdAsync(999);

        // Assert
        result.Should().BeNull("no user with this ID exists in the repository");
    }

    // -----------------------------------------------------------------------
    // UPDATE — test 6
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Update 1 — Valid update: returns refreshed UserResponseDto with new values")]
    public async Task UpdateUserAsync_ValidId_ReturnsUpdatedDto()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        // Simulates the DB confirming the row was found and updated
        repoMock
            .Setup(r => r.UpdateAsync(1, "Admin", true))
            .ReturnsAsync(true);

        // Simulates re-fetching the updated record
        var updatedUser = SampleUser();
        updatedUser.Role     = "Admin";
        updatedUser.IsActive = true;

        repoMock
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(updatedUser);

        // Act
        var result = await sut.UpdateUserAsync(1, "Admin", true);

        // Assert
        result.Should().NotBeNull();

        result!.UserId.Should().Be(1);
        result.Role.Should().Be("Admin");
        result.IsActive.Should().BeTrue();

        repoMock.Verify(r => r.UpdateAsync(1, "Admin", true), Times.Once);
    }

    // -----------------------------------------------------------------------
    // UPDATE — test 7
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Update 2 — Invalid ID: throws KeyNotFoundException")]
    public async Task UpdateUserAsync_InvalidId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        // Repository reports that no row was affected (user not found)
        repoMock
            .Setup(r => r.UpdateAsync(999, It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(false);

        // Act
        Func<Task> act = async () => await sut.UpdateUserAsync(999, "Admin", true);

        // Assert
        await act.Should()
            .ThrowAsync<KeyNotFoundException>(
                because: "updating a non-existent user must surface as an explicit exception")
            .WithMessage("*999*");

        // Confirm no follow-up DB read was attempted after the failure
        repoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // DELETE — test 8
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Delete 1 — Soft delete: returns true and is_active becomes false")]
    public async Task DeleteUserAsync_ValidId_SoftDeletesUserAndReturnsTrue()
    {
        // Arrange
        var (repoMock, sut) = BuildSut();

        // Repository confirms the soft-delete was applied
        repoMock
            .Setup(r => r.DeleteAsync(1))
            .ReturnsAsync(true);

        // When the record is re-fetched after deletion it shows IsActive = false
        var softDeletedUser = SampleUser();
        softDeletedUser.IsActive = false;

        repoMock
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(softDeletedUser);

        // Act
        var success = await sut.DeleteUserAsync(1);

        // Assert — service reports success
        success.Should().BeTrue("the repository confirmed the soft-delete was applied");

        // Repository was asked to delete exactly once
        repoMock.Verify(r => r.DeleteAsync(1), Times.Once);

        // Confirm the record reflects is_active = false after deletion
        var userAfterDelete = await sut.GetUserByIdAsync(1);
        userAfterDelete!.IsActive.Should().BeFalse(
            "soft-delete must set is_active = false rather than removing the row");
    }
}
