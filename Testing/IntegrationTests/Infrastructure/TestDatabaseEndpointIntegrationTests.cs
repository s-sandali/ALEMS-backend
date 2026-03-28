using System.Net;
using System.Text.Json;
using backend.Data;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySql.Data.MySqlClient;
using Xunit;

namespace IntegrationTests.Infrastructure;

public class TestDatabaseEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TestDatabaseEndpointIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonDocument> ParseBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        return JsonDocument.Parse(body);
    }

    [Fact(DisplayName = "BE-IT-DB-01 — GET /api/test/test-db returns 200 with server and database details when connection succeeds")]
    public async Task TestDb_Returns200_WithServerAndDatabaseDetails_OnSuccess()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DatabaseHelper>();
                services.AddScoped<DatabaseHelper, SuccessfulDatabaseHelper>();
            });
        });

        var client = app.CreateClient();
        var response = await client.GetAsync("/api/test/test-db");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Database connection is healthy.");
        doc.RootElement.GetProperty("server").GetString().Should().Contain("fake-server");
        doc.RootElement.GetProperty("database").GetString().Should().Be("fake-db");
    }

    [Fact(DisplayName = "BE-IT-DB-02 — GET /api/test/test-db returns structured 500 for MySQL-specific failures")]
    public async Task TestDb_ReturnsStructured500_ForMySqlFailures()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DatabaseHelper>();
                services.AddScoped<DatabaseHelper, MySqlFailureDatabaseHelper>();
            });
        });

        var client = app.CreateClient();
        var response = await client.GetAsync("/api/test/test-db");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Database connection failed.");
        doc.RootElement.TryGetProperty("errorCode", out var errorCode).Should().BeTrue();
        errorCode.GetInt32().Should().NotBe(0);
        doc.RootElement.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "BE-IT-DB-03 — GET /api/test/test-db returns structured 500 for unexpected non-MySQL failures")]
    public async Task TestDb_ReturnsStructured500_ForUnexpectedNonMySqlFailures()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DatabaseHelper>();
                services.AddScoped<DatabaseHelper, UnexpectedFailureDatabaseHelper>();
            });
        });

        var client = app.CreateClient();
        var response = await client.GetAsync("/api/test/test-db");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should()
            .Be("An unexpected error occurred while testing the database connection.");
        doc.RootElement.GetProperty("detail").GetString().Should().Contain("simulated non-mysql failure");
    }

    private sealed class SuccessfulDatabaseHelper : DatabaseHelper
    {
        public SuccessfulDatabaseHelper()
            : base(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=fake-server;Database=fake-db;User ID=fake-user;Password=fake-pass;"
                })
                .Build())
        {
        }

        public override Task<MySqlConnection> OpenConnectionAsync()
        {
            var connection = new MySqlConnection(
                "Server=fake-server;Database=fake-db;User ID=fake-user;Password=fake-pass;");
            return Task.FromResult(connection);
        }
    }

    private sealed class MySqlFailureDatabaseHelper : DatabaseHelper
    {
        public MySqlFailureDatabaseHelper()
            : base(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=127.0.0.1;Port=1;Database=fake-db;User ID=fake-user;Password=fake-pass;Connection Timeout=1;"
                })
                .Build())
        {
        }

        public override async Task<MySqlConnection> OpenConnectionAsync()
        {
            var connection = new MySqlConnection(
                "Server=127.0.0.1;Port=1;Database=fake-db;User ID=fake-user;Password=fake-pass;Connection Timeout=1;");
            await connection.OpenAsync();
            return connection;
        }
    }

    private sealed class UnexpectedFailureDatabaseHelper : DatabaseHelper
    {
        public UnexpectedFailureDatabaseHelper()
            : base(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=fake-server;Database=fake-db;User ID=fake-user;Password=fake-pass;"
                })
                .Build())
        {
        }

        public override Task<MySqlConnection> OpenConnectionAsync()
        {
            throw new InvalidOperationException("simulated non-mysql failure");
        }
    }
}