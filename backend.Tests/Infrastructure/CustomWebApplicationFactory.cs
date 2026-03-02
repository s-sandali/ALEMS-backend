using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using backend.Services;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> for integration tests.
///
/// What it overrides:
/// • Authentication  — replaces Clerk JWT (OIDC) with <see cref="TestAuthHandler"/>
///   so tests never reach the Clerk JWKS endpoint.
/// • <see cref="IUserService"/> — replaced with <see cref="StubUserService"/>
///   so no real database calls are made during auth tests.
/// • Configuration   — injects test-safe values for Clerk:Authority and the
///   DB connection string so startup doesn't throw.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        // ── Safe config values to prevent startup exceptions ──────────────────
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Satisfies the "Clerk:Authority is required" guard in Program.cs
                ["Clerk:Authority"] = "https://test.clerk.example.com",
                // Satisfies DatabaseHelper constructor (no real connection is opened)
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=test_db;User=test;Password=test;"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── Replace Clerk JwtBearer with a deterministic test auth scheme ───
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme    = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            // ── Stub IUserService to avoid hitting MySQL ───────────────────────
            services.RemoveAll<IUserService>();
            services.AddScoped<IUserService, StubUserService>();
        });
    }
}
