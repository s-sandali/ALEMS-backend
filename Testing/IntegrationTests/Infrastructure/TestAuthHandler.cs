using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Test-only authentication handler.
///
/// Token map:
///   "test-admin-token"   → authenticated, Role = Admin
///   "test-student-token" → authenticated, Role = Student
///   "test-valid-token"   → authenticated, no role claim
///   anything else        → 401 Unauthorized
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName    = "TestAuth";
    public const string AdminToken    = "test-admin-token";
    public const string StudentToken  = "test-student-token";
    public const string ValidToken    = "test-valid-token";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var raw = authHeader.ToString();
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Missing Bearer prefix."));

        var token = raw["Bearer ".Length..].Trim();

        if (token != AdminToken && token != StudentToken && token != ValidToken)
            return Task.FromResult(AuthenticateResult.Fail("Token is invalid."));

        string? role = token switch
        {
            AdminToken   => "Admin",
            StudentToken => "Student",
            _            => null
        };

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,  "test-user"),
            new("sub",            "clerk_test_001"),
            new(ClaimTypes.Email, "test@example.com")
        };

        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
