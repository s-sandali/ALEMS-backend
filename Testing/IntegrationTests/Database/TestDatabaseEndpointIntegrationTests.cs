using System.Net;
using System.Text.Json;
using backend.Data;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using MySql.Data.MySqlClient;
using Xunit;

namespace IntegrationTests.Database;

file sealed class TestDatabaseWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _mode;

    public TestDatabaseWebApplicationFactory(string mode)
    {
        _mode = mode;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clerk:Authority"] = "https://test.clerk.example.com",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Port=3306;Database=test_db;User=test;Password=test;"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DatabaseHelper>();
            services.AddScoped<DatabaseHelper>(sp =>
            {
                var cfg = sp.GetRequiredService<IConfiguration>();
                var mock = new Mock<DatabaseHelper>(cfg);

                if (_mode == "success")
                {
                    mock.Setup(d => d.OpenConnectionAsync())
                        .ReturnsAsync(new MySqlConnection("Server=fake-server;Database=fake-db;User Id=u;Password=p;"));
                }
                else if (_mode == "mysql-failure")
                {
                    mock.Setup(d => d.OpenConnectionAsync())
                        .Returns(async () =>
                        {
                            try
                            {
                                await using var c = new MySqlConnection(
                                    "Server=127.0.0.1;Port=65000;Database=missing;User Id=u;Password=p;Connection Timeout=1;");
                                await c.OpenAsync();
                                return c;
                            }
                            catch (MySqlException)
                            {
                                throw;
                            }
                        });
                }
                else
                {
                    mock.Setup(d => d.OpenConnectionAsync())
                        .ThrowsAsync(new InvalidOperationException("Simulated unexpected failure."));
                }

                return mock.Object;
            });
        });
    }
}

public class TestDatabaseEndpointIntegrationTests
{
    [Fact(DisplayName = "BE-IT-DB-01 - GET /api/test/test-db returns 200 with server and database details")]
    public async Task TestDb_WhenConnectionSucceeds_ReturnsSuccessEnvelope()
    {
        await using var factory = new TestDatabaseWebApplicationFactory("success");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/test/test-db");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("message").GetString().Should().Be("Database connection is healthy.");
        root.GetProperty("server").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("database").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "BE-IT-DB-02 - GET /api/test/test-db returns structured 500 for MySQL failures")]
    public async Task TestDb_WhenMySqlFailureOccurs_ReturnsStructured500WithErrorCode()
    {
        await using var factory = new TestDatabaseWebApplicationFactory("mysql-failure");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/test/test-db");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Be("Database connection failed.");
        root.TryGetProperty("errorCode", out var errorCode).Should().BeTrue();
        errorCode.ValueKind.Should().Be(JsonValueKind.Number);
        root.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "BE-IT-DB-03 - GET /api/test/test-db returns structured 500 for non-MySQL failures")]
    public async Task TestDb_WhenUnexpectedFailureOccurs_ReturnsStructured500GenericMessage()
    {
        await using var factory = new TestDatabaseWebApplicationFactory("unexpected-failure");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/test/test-db");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("message").GetString()
            .Should().Be("An unexpected error occurred while testing the database connection.");
        root.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        root.TryGetProperty("errorCode", out _).Should().BeFalse();
    }
}
