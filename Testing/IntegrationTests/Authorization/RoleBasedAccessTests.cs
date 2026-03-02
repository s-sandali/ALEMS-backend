using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.Authorization;

/// <summary>
/// Role-based access control tests (SA-37).
///
/// Protected endpoint: GET /api/users  [Authorize(Roles = "Admin")]
///
/// TC-01  Student role → HTTP 403 Forbidden  (authenticated, wrong role)
/// TC-02  Admin role   → HTTP 200 OK         (authenticated, correct role)
/// </summary>
public class RoleBasedAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleBasedAccessTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── TC-01 — Student → 403 ────────────────────────────────────────────

    [Fact(DisplayName = "TC-01 — Student role: Admin endpoint returns HTTP 403 Forbidden")]
    public async Task AdminEndpoint_WithStudentRole_Returns403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.StudentToken);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "a Student must not access an Admin-only endpoint");
    }

    // ── TC-02 — Admin → 200 ──────────────────────────────────────────────

    [Fact(DisplayName = "TC-02 — Admin role: Admin endpoint returns HTTP 200 OK")]
    public async Task AdminEndpoint_WithAdminRole_Returns200()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "an Admin must be granted access to an Admin-only endpoint");
    }
}
