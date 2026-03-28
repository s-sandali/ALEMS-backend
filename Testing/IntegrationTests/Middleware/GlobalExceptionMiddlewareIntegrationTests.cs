using System.Net;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Middleware;

public class GlobalExceptionMiddlewareIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GlobalExceptionMiddlewareIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "BE-IT-MW-01 — Global exception middleware returns standardized 500 JSON with traceId for uncaught exception")]
    public async Task UncaughtExceptionPath_ReturnsStandardized500_WithTraceId()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddControllers()
                    .PartManager.ApplicationParts.Add(new AssemblyPart(typeof(TestOnlyThrowController).Assembly));
            });
        });

        var client = app.CreateClient();
        var response = await client.GetAsync("/api/test-only/throw");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotBeNullOrWhiteSpace();

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(500);
        root.GetProperty("message").GetString().Should().Be("An unexpected error occurred. Please try again later.");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}