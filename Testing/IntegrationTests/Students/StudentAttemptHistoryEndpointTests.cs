using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using backend.DTOs;
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

namespace IntegrationTests.Students;

public class StudentAttemptHistoryEndpointTests
{
    [Fact(DisplayName = "BE-IT-ST-AT-01 — GET /api/students/{id}/attempts returns correct history for known student")]
    public async Task GetAttemptHistory_WithKnownStudent_ReturnsCorrectHistory()
    {
        const int studentId = 42;

        var attempts = new List<UserAttemptHistoryDto>
        {
            new()
            {
                AttemptId = 9001,
                QuizId = 11,
                QuizTitle = "Quick Sort Basics",
                AlgorithmName = "Quick Sort",
                Score = 9,
                XpEarned = 30,
                Passed = true,
                StartedAt = new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 4, 10, 9, 10, 0, DateTimeKind.Utc)
            },
            new()
            {
                AttemptId = 9002,
                QuizId = 12,
                QuizTitle = "Merge Sort Intermediate",
                AlgorithmName = "Merge Sort",
                Score = 7,
                XpEarned = 20,
                Passed = true,
                StartedAt = new DateTime(2026, 4, 8, 8, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 4, 8, 8, 12, 0, DateTimeKind.Utc)
            }
        };

        using var factory = new StudentAttemptHistoryEndpointWebApplicationFactory(
            user: new UserResponseDto
            {
                UserId = studentId,
                ClerkUserId = "clerk_known_42",
                Email = "known.student@example.com",
                Username = "known-student",
                Role = "User",
                IsActive = true
            },
            totalAttempts: attempts.Count,
            attempts: attempts);

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync($"/api/students/{studentId}/attempts?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");

        data.GetProperty("page").GetInt32().Should().Be(1);
        data.GetProperty("pageSize").GetInt32().Should().Be(10);
        data.GetProperty("totalAttempts").GetInt32().Should().Be(2);
        data.GetProperty("totalPages").GetInt32().Should().Be(1);
        data.GetProperty("hasNextPage").GetBoolean().Should().BeFalse();
        data.GetProperty("hasPreviousPage").GetBoolean().Should().BeFalse();

        var resultAttempts = data.GetProperty("attempts").EnumerateArray().ToList();
        resultAttempts.Should().HaveCount(2);

        resultAttempts[0].GetProperty("attemptId").GetInt32().Should().Be(9001);
        resultAttempts[0].GetProperty("quizTitle").GetString().Should().Be("Quick Sort Basics");
        resultAttempts[0].GetProperty("algorithmName").GetString().Should().Be("Quick Sort");
        resultAttempts[0].GetProperty("score").GetInt32().Should().Be(9);
        resultAttempts[0].GetProperty("xpEarned").GetInt32().Should().Be(30);
        resultAttempts[0].GetProperty("passed").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-ST-AT-02 — GET /api/students/{id}/attempts returns 403 for Student token")]
    public async Task GetAttemptHistory_WithStudentToken_ReturnsForbidden()
    {
        using var factory = new StudentAttemptHistoryEndpointWebApplicationFactory();
        var client = BuildAuthorizedClient(factory, TestAuthHandler.UserToken);

        var response = await client.GetAsync("/api/students/42/attempts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-ST-AT-03 — GET /api/students/{id}/attempts returns empty history response")]
    public async Task GetAttemptHistory_WithNoAttempts_ReturnsEmptyHistoryResponse()
    {
        const int studentId = 50;

        using var factory = new StudentAttemptHistoryEndpointWebApplicationFactory(
            user: new UserResponseDto
            {
                UserId = studentId,
                ClerkUserId = "clerk_empty_50",
                Email = "empty.student@example.com",
                Username = "empty-student",
                Role = "User",
                IsActive = true
            },
            totalAttempts: 0,
            attempts: Array.Empty<UserAttemptHistoryDto>());

        var client = BuildAuthorizedClient(factory, TestAuthHandler.AdminToken);

        var response = await client.GetAsync($"/api/students/{studentId}/attempts?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        var data = root.GetProperty("data");

        data.GetProperty("attempts").EnumerateArray().Should().BeEmpty();
        data.GetProperty("totalAttempts").GetInt32().Should().Be(0);
        data.GetProperty("totalPages").GetInt32().Should().Be(0);
        data.GetProperty("hasNextPage").GetBoolean().Should().BeFalse();
        data.GetProperty("hasPreviousPage").GetBoolean().Should().BeFalse();
    }

    private static HttpClient BuildAuthorizedClient(WebApplicationFactory<Program> factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public sealed class StudentAttemptHistoryEndpointWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly UserResponseDto? _user;
    private readonly IReadOnlyCollection<UserAttemptHistoryDto> _attempts;
    private readonly int _totalAttempts;

    public StudentAttemptHistoryEndpointWebApplicationFactory(
        UserResponseDto? user = null,
        int totalAttempts = 0,
        IEnumerable<UserAttemptHistoryDto>? attempts = null)
    {
        _user = user;
        _totalAttempts = totalAttempts;
        _attempts = (attempts ?? Array.Empty<UserAttemptHistoryDto>()).ToList();
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

            var userServiceMock = new Mock<IUserService>();
            userServiceMock
                .Setup(s => s.GetUserByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int userId) => _user is not null && _user.UserId == userId ? _user : null);

            var attemptServiceMock = new Mock<IQuizAttemptService>();
            attemptServiceMock
                .Setup(s => s.GetUserAttemptHistoryAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync((int _, int page, int pageSize) => new StudentAttemptHistoryResponseDto
                {
                    Attempts = _attempts,
                    Page = page,
                    PageSize = pageSize,
                    TotalAttempts = _totalAttempts
                });

            services.RemoveAll<IUserService>();
            services.AddScoped(_ => userServiceMock.Object);

            services.RemoveAll<IQuizAttemptService>();
            services.AddScoped(_ => attemptServiceMock.Object);
        });
    }
}
