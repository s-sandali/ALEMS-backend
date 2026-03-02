using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using backend.Services;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// <see cref="WebApplicationFactory{TEntryPoint}"/> for integration tests.
///
/// Overrides:
/// • Authentication  — replaces Clerk JWT with <see cref="TestAuthHandler"/>
/// • <see cref="IUserService"/> — replaced with <see cref="StubUserService"/> (no DB)
/// • Configuration   — injects test-safe Clerk authority + connection string
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clerk:Authority"]                    = "https://test.clerk.example.com",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=test_db;User=test;Password=test;"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace Clerk JwtBearer with the deterministic test scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme    = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            // Swap IUserService for a no-op stub
            services.RemoveAll<IUserService>();
            services.AddScoped<IUserService, StubUserService>();
        });
    }
}
