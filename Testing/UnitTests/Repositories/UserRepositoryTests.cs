using backend.Data;
using backend.Models;
using backend.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace UnitTests.Repositories;

/// <summary>
/// Unit tests for <see cref="UserRepository"/>.
///
/// <para>
/// <see cref="UserRepository"/> uses raw ADO.NET via <see cref="DatabaseHelper"/>.
/// Because <see cref="MySqlConnection"/> and <see cref="MySqlDataReader"/> are sealed
/// concrete types, happy-path scenarios (read rows, map User objects) require a
/// real MySQL server and belong in integration tests.
/// </para>
///
/// <para>
/// What IS testable here: the repository correctly propagates exceptions thrown by
/// <see cref="DatabaseHelper.OpenConnectionAsync"/> to its caller without swallowing
/// them. Each public method is covered for the error path; the constructor is also
/// exercised.
/// </para>
///
/// Scenarios covered
/// -----------------
///   1. Constructor          — object is created without throwing
///   2. GetAllAsync          — DB failure propagates as exception
///   3. GetByIdAsync         — DB failure propagates as exception
///   4. GetByEmailAsync      — DB failure propagates as exception
///   5. GetByClerkUserIdAsync — DB failure propagates as exception
///   6. CreateAsync          — DB failure propagates as exception
///   7. UpdateAsync          — DB failure propagates as exception
///   8. DeleteAsync          — DB failure propagates as exception
/// </summary>
public class UserRepositoryTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IConfiguration FakeConfig => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=fake;Database=fake;User=fake;Password=fake;"
        })
        .Build();

    /// <summary>
    /// Builds a <see cref="UserRepository"/> whose <see cref="DatabaseHelper"/>
    /// has <c>OpenConnectionAsync</c> mocked to throw a <see cref="MySqlException"/>.
    /// </summary>
    private static (UserRepository repo, Mock<DatabaseHelper> dbMock) BuildRepo()
    {
        var dbMock = new Mock<DatabaseHelper>(FakeConfig);
        dbMock.Setup(d => d.OpenConnectionAsync())
              .ThrowsAsync(new InvalidOperationException("Simulated DB connection failure."));

        return (new UserRepository(dbMock.Object), dbMock);
    }

    // ── Scenario 1 — Constructor ──────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 1 — UserRepository: constructor succeeds with a valid DatabaseHelper")]
    public void Constructor_ValidDatabaseHelper_CreatesInstance()
    {
        var dbMock = new Mock<DatabaseHelper>(FakeConfig);

        var repo = new UserRepository(dbMock.Object);

        repo.Should().NotBeNull();
    }

    // ── Scenario 2 — GetAllAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 2 — GetAllAsync: DB failure propagates exception to caller")]
    public async Task GetAllAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();

        await repo.Invoking(r => r.GetAllAsync())
                  .Should().ThrowAsync<Exception>(
                      because: "a DB failure must not be silently swallowed by the repository");
    }

    // ── Scenario 3 — GetByIdAsync ─────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 3 — GetByIdAsync: DB failure propagates exception to caller")]
    public async Task GetByIdAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();

        await repo.Invoking(r => r.GetByIdAsync(1))
                  .Should().ThrowAsync<Exception>();
    }

    // ── Scenario 4 — GetByEmailAsync ──────────────────────────────────────────

    [Fact(DisplayName = "Scenario 4 — GetByEmailAsync: DB failure propagates exception to caller")]
    public async Task GetByEmailAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();

        await repo.Invoking(r => r.GetByEmailAsync("alice@example.com"))
                  .Should().ThrowAsync<Exception>();
    }

    // ── Scenario 5 — GetByClerkUserIdAsync ───────────────────────────────────

    [Fact(DisplayName = "Scenario 5 — GetByClerkUserIdAsync: DB failure propagates exception to caller")]
    public async Task GetByClerkUserIdAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();

        await repo.Invoking(r => r.GetByClerkUserIdAsync("clerk_001"))
                  .Should().ThrowAsync<Exception>();
    }

    // ── Scenario 6 — CreateAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 6 — CreateAsync: DB failure propagates exception to caller")]
    public async Task CreateAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();
        var user = new User
        {
            Email    = "bob@example.com",
            Username = "bob",
            Role     = "Student",
            IsActive = true
        };

        await repo.Invoking(r => r.CreateAsync(user))
                  .Should().ThrowAsync<Exception>();
    }

    // ── Scenario 7 — UpdateAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 7 — UpdateAsync: DB failure propagates exception to caller")]
    public async Task UpdateAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();

        await repo.Invoking(r => r.UpdateAsync(1, "Admin", true))
                  .Should().ThrowAsync<Exception>();
    }

    // ── Scenario 8 — DeleteAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Scenario 8 — DeleteAsync: DB failure propagates exception to caller")]
    public async Task DeleteAsync_DbFailure_PropagatesException()
    {
        var (repo, _) = BuildRepo();

        await repo.Invoking(r => r.DeleteAsync(1))
                  .Should().ThrowAsync<Exception>();
    }
}
