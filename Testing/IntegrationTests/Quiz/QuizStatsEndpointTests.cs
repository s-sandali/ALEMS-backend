using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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

namespace IntegrationTests.Quiz;

public class QuizStatsEndpointTests
{
    [Fact(DisplayName = "BE-IT-QS-01 — GET /api/quizzes/{id}/stats returns correct stats for known quiz")]
    public async Task GetQuizStats_KnownQuiz_ReturnsCorrectStats()
    {
        using var factory = new QuizStatsEndpointWebApplicationFactory(
            quizResolver: id => id == 101
                ? new backend.Models.Quiz { QuizId = 101, AlgorithmId = 1, CreatedBy = 1, Title = "Known quiz", PassScore = 70, IsActive = true }
                : null,
            attempts:
            [
                new QuizAttempt { AttemptId = 1, QuizId = 101, UserId = 10, Score = 80, Passed = true },
                new QuizAttempt { AttemptId = 2, QuizId = 101, UserId = 11, Score = 60, Passed = false },
                new QuizAttempt { AttemptId = 3, QuizId = 999, UserId = 12, Score = 100, Passed = true },
            ]);

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/quizzes/101/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");

        data.GetProperty("attemptCount").GetInt32().Should().Be(2);
        data.GetProperty("averageScore").GetDouble().Should().BeApproximately(70.0, 0.0001);
        data.GetProperty("passRate").GetDouble().Should().BeApproximately(50.0, 0.0001);
    }

    [Fact(DisplayName = "BE-IT-QS-02 — GET /api/quizzes/{id}/stats returns 403 on student token")]
    public async Task GetQuizStats_StudentToken_Returns403()
    {
        using var factory = new QuizStatsEndpointWebApplicationFactory();
        var client = BuildAuthorizedClient(factory, TestAuthHandler.UserToken);

        var response = await client.GetAsync("/api/quizzes/101/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-QS-03 — GET /api/quizzes/{id}/stats returns 404 for non-existent quiz")]
    public async Task GetQuizStats_NonExistentQuiz_Returns404()
    {
        using var factory = new QuizStatsEndpointWebApplicationFactory(
            quizResolver: _ => null,
            attempts: Array.Empty<QuizAttempt>());

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync("/api/quizzes/99999/stats");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("Quiz with ID 99999 not found");
    }

    private static HttpClient BuildAuthorizedClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class QuizStatsEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Func<int, backend.Models.Quiz?> _quizResolver;
    private readonly IReadOnlyCollection<QuizAttempt> _attempts;

    public QuizStatsEndpointWebApplicationFactory(
        Func<int, backend.Models.Quiz?>? quizResolver = null,
        IEnumerable<QuizAttempt>? attempts = null)
    {
        _quizResolver = quizResolver ?? (_ => new backend.Models.Quiz { QuizId = 101, AlgorithmId = 1, CreatedBy = 1, Title = "Default quiz", PassScore = 70, IsActive = true });
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

            var quizRepositoryMock = new Mock<IQuizRepository>();
            quizRepositoryMock
                .Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => _quizResolver(id));

            var attemptRepositoryMock = new Mock<IQuizAttemptRepository>();
            attemptRepositoryMock
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(_attempts);

            var userRepositoryMock = new Mock<IUserRepository>();
            var algorithmRepositoryMock = new Mock<IAlgorithmRepository>();

            services.RemoveAll<IQuizRepository>();
            services.AddScoped(_ => quizRepositoryMock.Object);

            services.RemoveAll<IQuizAttemptRepository>();
            services.AddScoped(_ => attemptRepositoryMock.Object);

            services.RemoveAll<IUserRepository>();
            services.AddScoped(_ => userRepositoryMock.Object);

            services.RemoveAll<IAlgorithmRepository>();
            services.AddScoped(_ => algorithmRepositoryMock.Object);
        });
    }
}
