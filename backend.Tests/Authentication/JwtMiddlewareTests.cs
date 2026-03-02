using System.Net;
using System.Net.Http.Headers;
using backend.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Authentication;

/// <summary>
/// Integration tests that verify the JWT authentication middleware (SA-36).
///
/// The protected endpoint under test: POST /api/users/sync  [Authorize]
///
/// Test Cases
/// ----------
/// TC-01  No Authorization header → HTTP 401 Unauthorized
/// TC-02  Invalid bearer token    → HTTP 401 Unauthorized
/// TC-03  Valid bearer token      → HTTP 2xx (200 or 201)
/// </summary>
public class JwtMiddlewareTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public JwtMiddlewareTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // TC-01 — No Authorization header → 401
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TC-01 — No auth header: protected endpoint returns HTTP 401")]
    public async Task ProtectedEndpoint_WithNoAuthHeader_Returns401()
    {
        // Arrange — fresh client, no Authorization header
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/users/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "requests without an Authorization header must be rejected");
    }

    // -------------------------------------------------------------------------
    // TC-02 — Invalid bearer token → 401
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TC-02 — Invalid token: protected endpoint returns HTTP 401")]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        // Arrange — fresh client with a deliberately wrong token
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "this-is-a-wrong-token");

        // Act
        var response = await client.PostAsync("/api/users/sync", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "an invalid bearer token must be rejected");
    }

    // -------------------------------------------------------------------------
    // TC-03 — Valid bearer token → 2xx
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "TC-03 — Valid token: protected endpoint returns HTTP 2xx")]
    public async Task ProtectedEndpoint_WithValidToken_Returns2xx()
    {
        // Arrange — fresh client with the well-known test token
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.ValidToken);

        // Act
        var response = await client.PostAsync("/api/users/sync", null);

        // Assert
        ((int)response.StatusCode).Should().BeInRange(200, 299,
            because: "a valid bearer token must pass authentication and reach the controller");
    }
}
