using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using backend.DTOs;
using backend.Models;
using backend.Repositories;
using backend.Services;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace IntegrationTests.Admin;

public class AdminLeaderboardEndpointTests
{
    [Fact(DisplayName = "BE-IT-ADM-LB-01 — GET /api/admin/leaderboard returns correct ranking order")]
    public async Task GetLeaderboard_WithAdminToken_ReturnsCorrectRankingOrder()
    {
        using var factory = new AdminLeaderboardEndpointWebApplicationFactory(
            users:
            [
                new User { UserId = 10, Username = "alpha", Email = "alpha@example.com", XpTotal = 300, Role = "User" },
                new User { UserId = 20, Username = "beta", Email = "beta@example.com", XpTotal = 300, Role = "User" },
                new User { UserId = 30, Username = "gamma", Email = "gamma@example.com", XpTotal = 120, Role = "User" },
                new User { UserId = 40, Username = "delta", Email = "delta@example.com", XpTotal = 50, Role = "User" },
            ],
            attempts:
            [
                new QuizAttempt { AttemptId = 1, UserId = 10, QuizId = 100, Score = 80, Passed = true },
                new QuizAttempt { AttemptId = 2, UserId = 10, QuizId = 101, Score = 60, Passed = true },
                new QuizAttempt { AttemptId = 3, UserId = 30, QuizId = 102, Score = 90, Passed = true },
            ]);

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/admin/leaderboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaderboard = await response.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>();
        leaderboard.Should().NotBeNull();
        var leaderboardEntries = leaderboard!;
        leaderboardEntries.Should().HaveCount(4);

        leaderboardEntries.Select(x => x.UserId).Should().Equal(10, 20, 30, 40);
        leaderboardEntries.Select(x => x.XpTotal).Should().BeInDescendingOrder();

        leaderboardEntries[0].Rank.Should().Be(1);
        leaderboardEntries[1].Rank.Should().Be(1);
        leaderboardEntries[2].Rank.Should().Be(3);
        leaderboardEntries[3].Rank.Should().Be(4);

        leaderboardEntries[0].AttemptCount.Should().Be(2);
        leaderboardEntries[0].AverageScore.Should().BeApproximately(70.0, 0.0001);
        leaderboardEntries[1].AttemptCount.Should().Be(0);
        leaderboardEntries[1].AverageScore.Should().Be(0);
        leaderboardEntries[2].AttemptCount.Should().Be(1);
        leaderboardEntries[2].AverageScore.Should().BeApproximately(90.0, 0.0001);
    }

    [Fact(DisplayName = "BE-IT-ADM-LB-02 — GET /api/admin/leaderboard returns 403 for student token")]
    public async Task GetLeaderboard_WithStudentToken_ReturnsForbidden()
    {
        using var factory = new AdminLeaderboardEndpointWebApplicationFactory();
        var client = BuildAuthorizedClient(factory, TestAuthHandler.UserToken);

        var response = await client.GetAsync("/api/admin/leaderboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-ADM-LB-03 — GET /api/admin/leaderboard returns empty response when no users exist")]
    public async Task GetLeaderboard_WithNoUsers_ReturnsEmptyResponse()
    {
        using var factory = new AdminLeaderboardEndpointWebApplicationFactory(
            users: Array.Empty<User>(),
            attempts: Array.Empty<QuizAttempt>());

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/admin/leaderboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var leaderboard = await response.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>();
        leaderboard.Should().NotBeNull();
        leaderboard.Should().BeEmpty();
    }

    private static HttpClient BuildAuthorizedClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class AdminLeaderboardEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly IReadOnlyCollection<User> _users;
    private readonly IReadOnlyCollection<QuizAttempt> _attempts;

    public AdminLeaderboardEndpointWebApplicationFactory(
        IEnumerable<User>? users = null,
        IEnumerable<QuizAttempt>? attempts = null)
    {
        _users = (users ?? Array.Empty<User>()).ToList();
        _attempts = (attempts ?? Array.Empty<QuizAttempt>()).ToList();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clerk:Authority"] = "https://test.clerk.example.com",
                ["Clerk:SecretKey"] = "sk_test_dummy_value_for_integration_tests",
                ["SkipMigrations"] = "true",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IUserService>();
            services.AddScoped<IUserService, StubUserService>();

            var userRepositoryMock = new Mock<IUserRepository>();
            userRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(_users);

            var attemptRepositoryMock = new Mock<IQuizAttemptRepository>();
            attemptRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(_attempts);

            services.RemoveAll<IUserRepository>();
            services.AddScoped(_ => userRepositoryMock.Object);

            services.RemoveAll<IQuizAttemptRepository>();
            services.AddScoped(_ => attemptRepositoryMock.Object);
        });
    }
}
