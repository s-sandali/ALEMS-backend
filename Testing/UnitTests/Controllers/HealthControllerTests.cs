using backend.Controllers;
using backend.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.Controllers;

/// <summary>
/// S1-US7 — Health Endpoint Testing (Unit-testing tier).
///
/// Tests <see cref="HealthController.GetHealth"/> in complete isolation —
/// no HTTP pipeline, no WebApplicationFactory, no DI container.
///
/// The only dependency, <see cref="DatabaseHelper"/>, is replaced by a
/// Moq stub whose virtual <c>PingAsync()</c> is configured per scenario:
///
/// <list type="table">
///   <listheader><term>Test</term><description>PingAsync behaviour → expected body</description></listheader>
///   <item><term>TC-HUT-01</term><description>Completes → status=Healthy, database=Connected</description></item>
///   <item><term>TC-HUT-02</term><description>Throws     → status=Degraded, database=Disconnected</description></item>
/// </list>
/// </summary>
public class HealthControllerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A real <see cref="IConfiguration"/> that satisfies
    /// <see cref="DatabaseHelper"/>'s constructor without a real MySQL server.
    /// PingAsync() is mocked so this connection string is never actually used.
    /// </summary>
    private static IConfiguration FakeConfig => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=fake;Database=fake_health;User=fake;Password=fake;"
        })
        .Build();

    /// <summary>
    /// Builds a <see cref="HealthController"/> whose <see cref="DatabaseHelper"/>
    /// dependency is replaced with a Moq stub.
    /// </summary>
    private static (HealthController controller, Mock<DatabaseHelper> dbMock) BuildController()
    {
        var dbMock     = new Mock<DatabaseHelper>(FakeConfig);
        var controller = new HealthController(dbMock.Object, NullLogger<HealthController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return (controller, dbMock);
    }

    /// <summary>
    /// Reads a property from the anonymous-type value returned by
    /// <see cref="OkObjectResult.Value"/> using reflection.
    /// </summary>
    private static T? GetProp<T>(object? value, string propertyName) =>
        (T?)value?.GetType().GetProperty(propertyName)?.GetValue(value);

    // ── TC-HUT-01 — DB Connected → Healthy ───────────────────────────────────

    [Fact(DisplayName = "TC-HUT-01 — DB connected: GetHealth returns 200 with status=Healthy")]
    public async Task GetHealth_WhenDbConnected_ReturnsHealthy()
    {
        // Arrange — PingAsync() completes successfully (no exception)
        var (controller, dbMock) = BuildController();
        dbMock.Setup(d => d.PingAsync()).Returns(Task.CompletedTask);

        // Act
        var actionResult = await controller.GetHealth();

        // Assert — always HTTP 200
        var result = actionResult as OkObjectResult;
        result.Should().NotBeNull(because: "GetHealth must always return OkObjectResult");
        result!.StatusCode.Should().Be(StatusCodes.Status200OK);

        // Assert body fields via reflection (anonymous type)
        var body = result.Value;
        GetProp<string>(body, "status")
            .Should().Be("Healthy",
                because: "a successful DB ping must yield status=Healthy");

        GetProp<string>(body, "database")
            .Should().Be("Connected",
                because: "a successful DB ping must report database=Connected");

        GetProp<DateTime>(body, "timestamp")
            .Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5),
                because: "timestamp must be the current UTC time");

        // Verify PingAsync was called exactly once
        dbMock.Verify(d => d.PingAsync(), Times.Once);
    }

    // ── TC-HUT-02 — DB Disconnected → Degraded ───────────────────────────────

    [Fact(DisplayName = "TC-HUT-02 — DB disconnected: GetHealth returns 200 with status=Degraded")]
    public async Task GetHealth_WhenDbDisconnected_ReturnsDegraded()
    {
        // Arrange — PingAsync() throws, simulating a connectivity failure
        var (controller, dbMock) = BuildController();
        dbMock.Setup(d => d.PingAsync())
              .ThrowsAsync(new InvalidOperationException("Simulated database failure."));

        // Act
        var actionResult = await controller.GetHealth();

        // Assert — must still return HTTP 200 (monitoring tools must not see 5xx)
        var result = actionResult as OkObjectResult;
        result.Should().NotBeNull(because: "GetHealth must return OkObjectResult even when DB is down");
        result!.StatusCode.Should().Be(StatusCodes.Status200OK,
            because: "the health endpoint must return 200 regardless of DB state " +
                     "so Azure App Service / uptime monitors can parse the degraded body");

        // Assert body fields via reflection
        var body = result.Value;
        GetProp<string>(body, "status")
            .Should().Be("Degraded",
                because: "a failed DB ping must yield status=Degraded");

        GetProp<string>(body, "database")
            .Should().Be("Disconnected",
                because: "a failed DB ping must report database=Disconnected");

        GetProp<DateTime>(body, "timestamp")
            .Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5),
                because: "timestamp must be present even in degraded state");

        // Verify PingAsync was called exactly once
        dbMock.Verify(d => d.PingAsync(), Times.Once);
    }
}
