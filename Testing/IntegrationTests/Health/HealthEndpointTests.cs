using System.Net;
using System.Text.Json;
using backend.Data;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace IntegrationTests.Health;

// ─────────────────────────────────────────────────────────────────────────────
//  Local WebApplicationFactory
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Configures the test host specifically for health-endpoint testing.
///
/// <para>Key overrides:</para>
/// <list type="bullet">
///   <item>Auth — replaced with <see cref="TestAuthHandler"/> (no real Clerk JWT needed).</item>
///   <item><see cref="DatabaseHelper"/> — replaced with a <see cref="Mock{T}"/> whose
///   <c>PingAsync()</c> is set up to either succeed (healthy) or throw (degraded),
///   controlled by the <paramref name="simulateDbFailure"/> constructor parameter.</item>
/// </list>
/// </summary>
file sealed class HealthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _simulateDbFailure;

    public HealthWebApplicationFactory(bool simulateDbFailure = false)
    {
        _simulateDbFailure = simulateDbFailure;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clerk:Authority"]                     = "https://test.clerk.example.com",
                // Fake connection string — never used because PingAsync is mocked
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=127.0.0.1;Database=fake_health_db;User=fake;Password=fake;"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── Replace Clerk JWT with the deterministic test scheme ──────────
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme    = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            // ── Replace DatabaseHelper with a Moq-based stub ──────────────────
            // PingAsync() is virtual so Moq can intercept it without needing MySQL.
            services.RemoveAll<DatabaseHelper>();
            services.AddScoped<DatabaseHelper>(sp =>
            {
                var cfg  = sp.GetRequiredService<IConfiguration>();
                var mock = new Mock<DatabaseHelper>(cfg);

                if (_simulateDbFailure)
                    mock.Setup(d => d.PingAsync())
                        .ThrowsAsync(new InvalidOperationException("Simulated database failure."));
                else
                    mock.Setup(d => d.PingAsync())
                        .Returns(Task.CompletedTask);

                return mock.Object;
            });
        });
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Tests
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// S1-US7 — Health Endpoint Testing.
///
/// Tests the <c>GET /api/health</c> endpoint under two database conditions:
///
/// <list type="table">
///   <listheader><term>Scenario</term><description>Expected body.status</description></listheader>
///   <item><term>DB connected</term><description>Healthy</description></item>
///   <item><term>DB disconnected</term><description>Degraded</description></item>
/// </list>
///
/// The endpoint always responds with <b>HTTP 200</b>; callers must inspect the
/// <c>status</c> field to determine the actual health state.
///
/// DB behaviour is simulated via <see cref="HealthWebApplicationFactory"/>, which
/// replaces the real <see cref="DatabaseHelper"/> with a Moq stub whose virtual
/// <c>PingAsync()</c> either completes successfully or throws, with no real MySQL
/// server required.
/// </summary>
public class HealthEndpointTests
{
    private static readonly JsonSerializerOptions _jsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── TC-HE-01 — DB connected → Healthy ────────────────────────────────

    [Fact(DisplayName = "TC-HE-01 — DB connected: /api/health returns 200 with status=Healthy")]
    public async Task Health_WhenDbConnected_ReturnsHealthy()
    {
        // Arrange — PingAsync() succeeds (no exception)
        await using var factory = new HealthWebApplicationFactory(simulateDbFailure: false);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health");

        // Assert — HTTP 200 always
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the health endpoint always returns 200 and encodes status in the body");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Overall status
        root.GetProperty("status").GetString()
            .Should().Be("Healthy",
                because: "a successful DB ping must yield status=Healthy");

        // DB sub-status
        root.GetProperty("database").GetString()
            .Should().Be("Connected",
                because: "a successful DB ping must report database=Connected");

        // Timestamp present and non-empty
        root.TryGetProperty("timestamp", out var ts).Should().BeTrue(
            because: "the health response must include a timestamp");
        ts.ValueKind.Should().NotBe(JsonValueKind.Null,
            because: "timestamp must not be null");
    }

    // ── TC-HE-02 — DB disconnected → Degraded ────────────────────────────

    [Fact(DisplayName = "TC-HE-02 — DB disconnected: /api/health returns 200 with status=Degraded")]
    public async Task Health_WhenDbDisconnected_ReturnsDegraded()
    {
        // Arrange — PingAsync() throws, simulating a DB connectivity failure
        await using var factory = new HealthWebApplicationFactory(simulateDbFailure: true);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/health");

        // Assert — HTTP 200 always (monitoring tools must not see 5xx)
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "the health endpoint must return 200 even when the DB is unreachable " +
                     "so that Azure App Service / uptime monitors can parse the degraded body");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Overall status
        root.GetProperty("status").GetString()
            .Should().Be("Degraded",
                because: "a failed DB ping must yield status=Degraded");

        // DB sub-status
        root.GetProperty("database").GetString()
            .Should().Be("Disconnected",
                because: "a failed DB ping must report database=Disconnected");

        // Timestamp still present even in degraded state
        root.TryGetProperty("timestamp", out var ts).Should().BeTrue(
            because: "the health response must include a timestamp in all states");
        ts.ValueKind.Should().NotBe(JsonValueKind.Null,
            because: "timestamp must not be null");
    }
}
