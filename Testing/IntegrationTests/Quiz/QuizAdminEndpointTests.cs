using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using backend.Services;
using Xunit;

namespace IntegrationTests.Quiz;

/// <summary>
/// Integration tests for the Admin quiz management API  —  /api/quizzes
///
/// Validates the full HTTP pipeline: routing → authentication → authorization
/// → model binding → validation → controller logic → response formatting.
/// Quiz services are stubbed; no database is required.
///
/// Endpoints covered
/// ───────────────────
/// GET    /api/quizzes
/// GET    /api/quizzes/{id}
/// POST   /api/quizzes
/// PUT    /api/quizzes/{id}
/// DELETE /api/quizzes/{id}
///
/// Test groups
/// ───────────
/// BE-IT-QUIZ-01..03   Authentication / Authorization enforcement
/// BE-IT-QUIZ-04..06   GET /api/quizzes  (list)
/// BE-IT-QUIZ-07..09   GET /api/quizzes/{id}
/// BE-IT-QUIZ-10..19   POST /api/quizzes  (create + validation)
/// BE-IT-QUIZ-20..24   PUT  /api/quizzes/{id}  (update + validation)
/// BE-IT-QUIZ-25..27   DELETE /api/quizzes/{id}
/// </summary>
public class QuizAdminEndpointTests : IClassFixture<QuizWebApplicationFactory>
{
    private readonly QuizWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _studentClient;
    private readonly HttpClient _anonClient;

    public QuizAdminEndpointTests(QuizWebApplicationFactory factory)
    {
        _factory = factory;

        _adminClient = factory.CreateClient();
        _adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        _studentClient = factory.CreateClient();
        _studentClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.UserToken);

        _anonClient = factory.CreateClient(); // no Authorization header
    }

    // —— Helpers ————————————————————————————————————————————————————————————————

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace("API must always return a JSON body");
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Asserts the standardised validation-error envelope:
    ///   { statusCode: 400, message: "Validation Failed", errors: { ... } }
    /// </summary>
    private static async Task AssertValidationErrorAsync(HttpResponseMessage response,
        string? expectedFieldKey = null)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "invalid input must be rejected with HTTP 400");

        using var doc = await ParseAsync(response);
        var root = doc.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(400);
        root.GetProperty("message").GetString().Should().Be("Validation Failed");
        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.ValueKind.Should().Be(JsonValueKind.Object);

        if (expectedFieldKey is not null)
            errorsEl.TryGetProperty(expectedFieldKey, out _).Should().BeTrue(
                because: $"errors must include the '{expectedFieldKey}' field");
    }

    private static async Task AssertSuccessAsync(HttpResponseMessage response, HttpStatusCode code)
    {
        response.StatusCode.Should().Be(code);
        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response,
        HttpStatusCode code, string? messageContains = null)
    {
        response.StatusCode.Should().Be(code);
        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");

        if (messageContains is not null)
            doc.RootElement.GetProperty("message").GetString()
               .Should().Contain(messageContains);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-01 — GET /api/quizzes: no token returns 401")]
    public async Task GetAll_NoToken_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/quizzes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-02 — GET /api/quizzes: Student token returns 403 (Admin-only route)")]
    public async Task GetAll_StudentToken_Returns403()
    {
        var response = await _studentClient.GetAsync("/api/quizzes");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-03 — POST /api/quizzes: Student token returns 403")]
    public async Task Create_StudentToken_Returns403()
    {
        var response = await _studentClient.PostAsJsonAsync("/api/quizzes", new
        {
            algorithmId = 1,
            title       = "Any Title"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-04 — GET /api/quizzes: Admin returns 200 with success envelope")]
    public async Task GetAll_Admin_Returns200WithSuccessEnvelope()
    {
        var response = await _adminClient.GetAsync("/api/quizzes");
        await AssertSuccessAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-05 — GET /api/quizzes: response data is an array")]
    public async Task GetAll_Admin_ResponseDataIsArray()
    {
        var response = await _adminClient.GetAsync("/api/quizzes");

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").ValueKind
           .Should().Be(JsonValueKind.Array, because: "quiz list must be a JSON array");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-06 — GET /api/quizzes: response contains quiz objects with required fields")]
    public async Task GetAll_Admin_ResponseContainsQuizObjectsWithRequiredFields()
    {
        var response = await _adminClient.GetAsync("/api/quizzes");

        using var doc = await ParseAsync(response);
        var first = doc.RootElement.GetProperty("data")
                        .EnumerateArray().First();

        first.TryGetProperty("quizId", out _).Should().BeTrue();
        first.TryGetProperty("title", out _).Should().BeTrue();
        first.TryGetProperty("algorithmId", out _).Should().BeTrue();
        first.TryGetProperty("passScore", out _).Should().BeTrue();
        first.TryGetProperty("isActive", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-QUIZ-07 — GET /api/quizzes/1: returns 200 with quiz data")]
    public async Task GetById_ExistingId_Returns200WithData()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1");
        await AssertSuccessAsync(response, HttpStatusCode.OK);

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").GetProperty("quizId").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("data").GetProperty("title").GetString()
           .Should().Be("Integration Test Quiz");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-08 — GET /api/quizzes/999: returns 404 with error envelope")]
    public async Task GetById_MissingId_Returns404()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-09 — GET /api/quizzes/999: no token returns 401")]
    public async Task GetById_NoToken_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/quizzes/999");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-10 — POST /api/quizzes: valid payload returns 201 Created")]
    public async Task Create_ValidPayload_Returns201()
    {
        var response = await _adminClient.PostAsJsonAsync("/api/quizzes", new
        {
            algorithmId = 1,
            title       = "Valid Quiz Title",
            description = "A quiz about sorting algorithms.",
            passScore   = 70
        });

        await AssertSuccessAsync(response, HttpStatusCode.Created);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-11 — POST /api/quizzes: 201 response includes data object with quizId and title")]
    public async Task Create_ValidPayload_ResponseIncludesCreatedData()
    {
        var response = await _adminClient.PostAsJsonAsync("/api/quizzes", new
        {
            algorithmId = 1,
            title       = "My New Quiz"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var doc = await ParseAsync(response);

        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("quizId", out _).Should().BeTrue();
        data.GetProperty("title").GetString().Should().Be("My New Quiz");
        doc.RootElement.GetProperty("message").GetString()
           .Should().Contain("created");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-12 — POST /api/quizzes: minimal payload (title + algorithmId only) returns 201")]
    public async Task Create_MinimalPayload_Returns201()
    {
        var response = await _adminClient.PostAsJsonAsync("/api/quizzes", new
        {
            algorithmId = 1,
            title       = "Minimal Quiz"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-13 — POST /api/quizzes: missing title returns 400 Validation Failed")]
    public async Task Create_MissingTitle_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes",
            Json("""{ "algorithmId": 1 }"""));

        await AssertValidationErrorAsync(response, "Title");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-14 — POST /api/quizzes: title too short (2 chars) returns 400")]
    public async Task Create_TitleTooShort_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes",
            Json("""{ "algorithmId": 1, "title": "AB" }"""));

        await AssertValidationErrorAsync(response, "Title");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-15 — POST /api/quizzes: algorithmId = 0 returns 400 (must be > 0)")]
    public async Task Create_AlgorithmIdZero_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes",
            Json("""{ "algorithmId": 0, "title": "Valid Title" }"""));

        await AssertValidationErrorAsync(response, "AlgorithmId");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-16 — POST /api/quizzes: passScore = 101 returns 400 (max 100)")]
    public async Task Create_PassScoreOverMax_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes",
            Json("""{ "algorithmId": 1, "title": "Quiz", "passScore": 101 }"""));

        await AssertValidationErrorAsync(response, "PassScore");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-17 — POST /api/quizzes: timeLimitMins = 0 returns 400 (min 1)")]
    public async Task Create_TimeLimitZero_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes",
            Json("""{ "algorithmId": 1, "title": "Quiz", "timeLimitMins": 0 }"""));

        await AssertValidationErrorAsync(response, "TimeLimitMins");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-18 — POST /api/quizzes: empty body returns 400 Validation Failed")]
    public async Task Create_EmptyBody_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes", Json("{}"));
        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-19 — POST /api/quizzes: service KeyNotFoundException (user not synced) returns 404")]
    public async Task Create_UserNotSynced_Returns404()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQuizService>();
                services.AddScoped<IQuizService, UserNotSyncedQuizService>();
            });
        });

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        var response = await client.PostAsJsonAsync("/api/quizzes", new
        {
            algorithmId = 1,
            title       = "Should Fail Quiz"
        });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "local account");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-19b — POST /api/quizzes: service ArgumentException (algorithm missing) returns 400")]
    public async Task Create_AlgorithmMissing_Returns400()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQuizService>();
                services.AddScoped<IQuizService, AlgorithmNotFoundQuizService>();
            });
        });

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        var response = await client.PostAsJsonAsync("/api/quizzes", new
        {
            algorithmId = 999,
            title       = "Should Fail Quiz"
        });

        await AssertErrorAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-20 — PUT /api/quizzes/1: valid payload returns 200 OK")]
    public async Task Update_ValidPayload_Returns200()
    {
        var response = await _adminClient.PutAsJsonAsync("/api/quizzes/1", new
        {
            title     = "Updated Quiz Title",
            passScore = 80,
            isActive  = true
        });

        await AssertSuccessAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-21 — PUT /api/quizzes/1: response contains updated title")]
    public async Task Update_ValidPayload_ResponseContainsUpdatedTitle()
    {
        var response = await _adminClient.PutAsJsonAsync("/api/quizzes/1", new
        {
            title     = "Renamed Quiz",
            passScore = 75,
            isActive  = false
        });

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").GetProperty("title").GetString()
           .Should().Be("Renamed Quiz");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-22 — PUT /api/quizzes/999: returns 404 Not Found")]
    public async Task Update_MissingId_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync("/api/quizzes/999", new
        {
            title     = "Irrelevant",
            passScore = 70,
            isActive  = true
        });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-23 — PUT /api/quizzes/1: missing title returns 400 Validation Failed")]
    public async Task Update_MissingTitle_Returns400()
    {
        var response = await _adminClient.PutAsync("/api/quizzes/1",
            Json("""{ "passScore": 70, "isActive": true }"""));

        await AssertValidationErrorAsync(response, "Title");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-24 — PUT /api/quizzes/1: Student token returns 403")]
    public async Task Update_StudentToken_Returns403()
    {
        var response = await _studentClient.PutAsJsonAsync("/api/quizzes/1", new
        {
            title    = "Hacked",
            isActive = false
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-QUIZ-25 — DELETE /api/quizzes/1: returns 204 No Content")]
    public async Task Delete_ExistingId_Returns204()
    {
        var response = await _adminClient.DeleteAsync("/api/quizzes/1");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().BeNullOrEmpty(because: "204 responses carry no body");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-26 — DELETE /api/quizzes/999: returns 404 Not Found")]
    public async Task Delete_MissingId_Returns404()
    {
        var response = await _adminClient.DeleteAsync("/api/quizzes/999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-QUIZ-27 — DELETE /api/quizzes/1: Student token returns 403")]
    public async Task Delete_StudentToken_Returns403()
    {
        var response = await _studentClient.DeleteAsync("/api/quizzes/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
