using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using backend.Services;
using backend.Models.Simulations;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegrationTests.Infrastructure;

file sealed class ThrowingAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ThrowingAuth";

    public ThrowingAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        throw new InvalidOperationException("Synthetic auth pipeline failure for middleware integration testing.");
    }
}

public class GlobalExceptionMiddlewarePipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GlobalExceptionMiddlewarePipelineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "BE-IT-MW-01 - Global exception middleware returns standardized 500 JSON with traceId")]
    public async Task UncaughtPipelineException_ReturnsStandardized500WithTraceId()
    {
        await using var failingFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Test");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = ThrowingAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ThrowingAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, ThrowingAuthHandler>(
                    ThrowingAuthHandler.SchemeName, _ => { });
            });
        });

        var client = failingFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "any-token");

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(500);
        root.GetProperty("message").GetString()
            .Should().Be("An unexpected error occurred. Please try again later.");
        root.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}
