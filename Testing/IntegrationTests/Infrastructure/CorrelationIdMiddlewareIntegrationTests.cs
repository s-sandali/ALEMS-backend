using System.Net.Http.Headers;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="backend.Middleware.CorrelationIdMiddleware"/>.
///
/// These tests run through the full ASP.NET Core pipeline (via
/// <see cref="CustomWebApplicationFactory"/>) to verify that the middleware's
/// <c>OnStarting</c> callback fires and echoes the <c>X-Correlation-ID</c>
/// response header — behaviour that cannot be triggered by a plain
/// <see cref="Microsoft.AspNetCore.Http.DefaultHttpContext"/> in unit tests.
/// </summary>
public class CorrelationIdMiddlewareIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly HttpClient _client;

    public CorrelationIdMiddlewareIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);
    }

    // ── TC-CID-I-01 — Provided correlation ID is echoed in the response ───────

    [Fact(DisplayName = "TC-CID-I-01 — Provided X-Correlation-ID is echoed back in the response header")]
    public async Task Request_WithCorrelationIdHeader_EchoesItInResponse()
    {
        const string correlationId = "test-correlation-abc-123";
        _client.DefaultRequestHeaders.TryAddWithoutValidation(HeaderName, correlationId);

        var response = await _client.GetAsync("/api/health");

        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue(
            because: "the middleware must always echo X-Correlation-ID in the response");

        values!.First().Should().Be(correlationId,
            because: "the echoed header must match the value sent in the request");
    }

    // ── TC-CID-I-02 — No incoming header: a new GUID is generated and echoed ──

    [Fact(DisplayName = "TC-CID-I-02 — No X-Correlation-ID in request: new GUID generated and returned")]
    public async Task Request_WithoutCorrelationIdHeader_GeneratesAndEchoesGuid()
    {
        // Ensure no correlation header is sent
        _client.DefaultRequestHeaders.Remove(HeaderName);

        var response = await _client.GetAsync("/api/health");

        response.Headers.TryGetValues(HeaderName, out var values).Should().BeTrue(
            because: "the middleware must generate a correlation ID when none is provided");

        var id = values!.First();
        id.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(id, out _).Should().BeTrue(
            because: "the auto-generated correlation ID must be a valid GUID");
    }
}
