using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace backend.Tests.Authentication;

/// <summary>
/// Unit tests for JWT validation middleware behavior (S1-US3).
///
/// Strategy
/// --------
/// A minimal ServiceCollection is built with AddAuthentication/AddJwtBearer
/// configured against a local HS256 symmetric key — no Clerk JWKS endpoint,
/// no WebApplicationFactory, no TestAuthHandler.
///
/// IAuthenticationService.AuthenticateAsync is called directly, exercising
/// the real JwtBearerHandler. If authentication does not succeed,
/// ChallengeAsync is called, which fires the OnChallenge event and writes 401.
///
/// Test Cases
/// ----------
/// TC-01  No Authorization header   → HTTP 401 Unauthorized
/// TC-02  Tampered / malformed token → HTTP 401 Unauthorized
/// TC-03  Properly signed JWT        → HTTP 200 OK (access granted)
/// </summary>
public class JwtMiddlewareTests
{
    // ── Constants ────────────────────────────────────────────────────────────

    private const string TestIssuer     = "https://test-clerk.example.com";

    // HS256 keys must be ≥ 128 bits; use 256-bit (32 char) for safety.
    private const string TestSigningKey = "unit-test-signing-key-32chars!!!";

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal <see cref="IServiceProvider"/> that configures the real
    /// <see cref="JwtBearerHandler"/> with a symmetric test key so no network
    /// calls are required.
    /// </summary>
    private static IServiceProvider BuildServiceProvider()
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));

        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = signingKey,

                    ValidateIssuer  = true,
                    ValidIssuer     = TestIssuer,

                    // Audience not configured in the real app when Clerk:Audience is absent
                    ValidateAudience = false,

                    ValidateLifetime = true,
                    ClockSkew        = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // Mirror the real OnChallenge from Program.cs:
                    // suppress the default header and return 401.
                    OnChallenge = ctx =>
                    {
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                };
            });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Returns a <see cref="DefaultHttpContext"/> wired to <paramref name="sp"/>
    /// with a writable response body.
    /// </summary>
    private static DefaultHttpContext CreateHttpContext(IServiceProvider sp)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = sp
        };
        context.Response.Body = new MemoryStream();
        return context;
    }

    /// <summary>
    /// Mints a real HS256-signed JWT accepted by the test service provider.
    /// </summary>
    private static string MintValidJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub",            "clerk_test_001"),
                new Claim(ClaimTypes.Email, "test@example.com")
            }),
            Issuer             = TestIssuer,
            Expires            = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    /// <summary>
    /// Runs the authentication pipeline directly:
    /// authenticates via the real <see cref="JwtBearerHandler"/>;
    /// challenges (→ 401) if the result is not a success;
    /// sets 200 when authentication succeeds.
    /// </summary>
    private static async Task InvokeAuthPipelineAsync(DefaultHttpContext ctx)
    {
        var authService = ctx.RequestServices.GetRequiredService<IAuthenticationService>();

        var result = await authService.AuthenticateAsync(ctx, JwtBearerDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            await authService.ChallengeAsync(ctx, JwtBearerDefaults.AuthenticationScheme, null);
        }
        else
        {
            ctx.Response.StatusCode = StatusCodes.Status200OK;
        }
    }

    // ── TC-01 — No Authorization header → 401 ────────────────────────────────

    [Fact(DisplayName = "TC-01 — Missing token: no Authorization header returns HTTP 401")]
    public async Task MissingToken_NoAuthorizationHeader_Returns401()
    {
        // Arrange
        var sp      = BuildServiceProvider();
        var context = CreateHttpContext(sp);
        // Intentionally: no Authorization header set

        // Act
        await InvokeAuthPipelineAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized,
            because: "a request without an Authorization header must be rejected with 401");
    }

    // ── TC-02 — Tampered / malformed token → 401 ─────────────────────────────

    [Fact(DisplayName = "TC-02 — Invalid token: tampered bearer value returns HTTP 401")]
    public async Task InvalidToken_TamperedBearer_Returns401()
    {
        // Arrange
        var sp      = BuildServiceProvider();
        var context = CreateHttpContext(sp);
        context.Request.Headers["Authorization"] = "Bearer this-is-not-a-valid-jwt-at-all";

        // Act
        await InvokeAuthPipelineAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized,
            because: "a malformed or tampered JWT must be rejected with 401");
    }

    // ── TC-03 — Valid signed JWT → 200 ───────────────────────────────────────

    [Fact(DisplayName = "TC-03 — Valid token: properly signed JWT with correct issuer grants access")]
    public async Task ValidToken_ProperlySignedJwt_GrantsAccess()
    {
        // Arrange
        var sp      = BuildServiceProvider();
        var context = CreateHttpContext(sp);
        context.Request.Headers["Authorization"] = $"Bearer {MintValidJwt()}";

        // Act
        await InvokeAuthPipelineAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK,
            because: "a properly signed JWT with a matching issuer and signing key must pass validation");
    }
}
