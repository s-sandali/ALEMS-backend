using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Tests.Infrastructure;

/// <summary>
/// A minimal authentication handler for integration tests.
/// Grants access to requests that carry the literal token <see cref="ValidToken"/>
/// and challenges (→ 401) everything else.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme name registered in the DI container.</summary>
    public const string SchemeName = "TestAuth";

    /// <summary>The exact bearer token that is treated as valid in tests.</summary>
    public const string ValidToken = "test-valid-token";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No Authorization header at all → unauthenticated (triggers challenge → 401)
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var raw = authHeader.ToString();

        // Must start with "Bearer "
        if (!raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Missing Bearer prefix."));

        var token = raw["Bearer ".Length..].Trim();

        // Wrong token → fail (triggers challenge → 401)
        if (token != ValidToken)
            return Task.FromResult(AuthenticateResult.Fail("Token is invalid."));

        // Valid token → build an authenticated ClaimsPrincipal
        var claims = new[]
        {
            new Claim(ClaimTypes.Name,  "test-user"),
            new Claim("sub",            "clerk_test_001"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity  = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
