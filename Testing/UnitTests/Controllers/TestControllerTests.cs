using backend.Controllers;
using backend.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MySql.Data.MySqlClient;
using System.Runtime.CompilerServices;
using Xunit;

namespace UnitTests.Controllers;

/// <summary>
/// Unit tests for <see cref="TestController"/>.
///
/// Scenarios covered
/// -----------------
///   1. DB connection succeeds   → 200 OK with connection details
///   2. MySqlException thrown    → 500 with structured error body
///   3. Generic exception thrown → 500 with structured error body
///
/// <see cref="DatabaseHelper.OpenConnectionAsync"/> is virtual so Moq can
/// intercept it without requiring a real MySQL server.
/// </summary>
public class TestControllerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration FakeConfig => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=fake_server;Database=fake_db;User=fake;Password=fake;"
        })
        .Build();

    private static (TestController controller, Mock<DatabaseHelper> dbMock) BuildController()
    {
        var dbMock     = new Mock<DatabaseHelper>(FakeConfig);
        var controller = new TestController(dbMock.Object, NullLogger<TestController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return (controller, dbMock);
    }

    private static T? GetProp<T>(object? value, string property) =>
        (T?)value?.GetType().GetProperty(property)?.GetValue(value);

    // ── Scenario 1 — DB connected → 200 OK ───────────────────────────────────

    [Fact(DisplayName = "Scenario 1 — TestDatabase: DB connected returns 200 with status=success")]
    public async Task TestDatabase_DbConnected_Returns200()
    {
        var (controller, dbMock) = BuildController();

        // Return a MySqlConnection built from a connection string (not actually opened).
        // DataSource and Database are readable from the connection string without a live connection.
        var fakeConn = new MySqlConnection(
            "Server=fake_server;Database=fake_db;User=fake;Password=fake;");
        dbMock.Setup(d => d.OpenConnectionAsync()).ReturnsAsync(fakeConn);

        // Act
        var result = await controller.TestDatabase() as OkObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status200OK);
        GetProp<string>(result.Value, "status").Should().Be("success");
        GetProp<string>(result.Value, "message").Should().Contain("healthy");

        dbMock.Verify(d => d.OpenConnectionAsync(), Times.Once);
    }

    // ── Scenario 2 — MySqlException → 500 ────────────────────────────────────

    [Fact(DisplayName = "Scenario 2 — TestDatabase: MySqlException returns 500 with status=error")]
    public async Task TestDatabase_MySqlException_Returns500()
    {
        var (controller, dbMock) = BuildController();

        // MySqlException 9.x has no public constructor; create an uninitialized instance
        // via RuntimeHelpers so it still passes the 'catch (MySqlException)' type check.
        var mysqlEx = (MySqlException)RuntimeHelpers.GetUninitializedObject(typeof(MySqlException));
        dbMock.Setup(d => d.OpenConnectionAsync()).ThrowsAsync(mysqlEx);

        // Act
        var result = await controller.TestDatabase() as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        GetProp<string>(result.Value, "status").Should().Be("error");
        GetProp<string>(result.Value, "message").Should().Contain("Database connection failed");
    }

    // ── Scenario 3 — Generic exception → 500 ─────────────────────────────────

    [Fact(DisplayName = "Scenario 3 — TestDatabase: unexpected exception returns 500 with status=error")]
    public async Task TestDatabase_GenericException_Returns500()
    {
        var (controller, dbMock) = BuildController();
        dbMock.Setup(d => d.OpenConnectionAsync())
              .ThrowsAsync(new InvalidOperationException("Something went wrong."));

        // Act
        var result = await controller.TestDatabase() as ObjectResult;

        // Assert
        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        GetProp<string>(result.Value, "status").Should().Be("error");
        GetProp<string>(result.Value, "message").Should().Contain("unexpected error");
    }
}
