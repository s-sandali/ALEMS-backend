using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using backend.Services;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests.Quiz;

/// <summary>
/// Integration tests for the Student-facing quiz API  —  /api/student/...
///
/// Validates the full HTTP pipeline for student routes: any authenticated user
/// (Student OR Admin) may access these endpoints. Correct answers and
/// explanations must never appear in question-list responses.
///
/// Endpoints covered
/// ───────────────────
/// GET  /api/student/quizzes
/// GET  /api/student/quizzes/{id}
/// GET  /api/student/quizzes/{quizId}/questions
/// POST /api/student/quizzes/{quizId}/attempt
///
/// Test groups
/// ───────────
/// BE-IT-SQ-01..03    Authentication (any authenticated user may access)
/// BE-IT-SQ-04..06    GET /api/student/quizzes
/// BE-IT-SQ-07..10    GET /api/student/quizzes/{id}
/// BE-IT-SQ-11..14    GET /api/student/quizzes/{quizId}/questions
/// BE-IT-SQ-15..27    POST /api/student/quizzes/{quizId}/attempt
/// </summary>
public class StudentQuizEndpointTests : IClassFixture<QuizWebApplicationFactory>
{
    private readonly QuizWebApplicationFactory _factory;
    private readonly HttpClient _studentClient;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _anonClient;

    public StudentQuizEndpointTests(QuizWebApplicationFactory factory)
    {
        _factory = factory;

        _studentClient = factory.CreateClient();
        _studentClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.UserToken);

        _adminClient = factory.CreateClient();
        _adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        _anonClient = factory.CreateClient();
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

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response,
        string? fieldKey = null)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = await ParseAsync(response);
        var root = doc.RootElement;
        root.GetProperty("statusCode").GetInt32().Should().Be(400);
        root.GetProperty("message").GetString().Should().Be("Validation Failed");
        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();

        if (fieldKey is not null)
            errorsEl.TryGetProperty(fieldKey, out _).Should().BeTrue(
                because: $"errors must include the '{fieldKey}' field");
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

    [Fact(DisplayName = "BE-IT-SQ-01 — GET /api/student/quizzes: no token returns 401")]
    public async Task GetActiveQuizzes_NoToken_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/student/quizzes");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-SQ-02 — GET /api/student/quizzes: Student token is accepted (200 OK)")]
    public async Task GetActiveQuizzes_StudentToken_Returns200()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-SQ-03 — GET /api/student/quizzes: Admin token is also accepted (200 OK)")]
    public async Task GetActiveQuizzes_AdminToken_Returns200()
    {
        var response = await _adminClient.GetAsync("/api/student/quizzes");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-SQ-04 — GET /api/student/quizzes: returns 200 with success envelope")]
    public async Task GetActiveQuizzes_Returns200WithSuccessEnvelope()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes");
        await AssertSuccessAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-SQ-05 — GET /api/student/quizzes: data is a JSON array")]
    public async Task GetActiveQuizzes_DataIsArray()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes");
        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").ValueKind
           .Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "BE-IT-SQ-06 — GET /api/student/quizzes: each quiz has required fields")]
    public async Task GetActiveQuizzes_QuizzesHaveRequiredFields()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes");
        using var doc = await ParseAsync(response);

        var first = doc.RootElement.GetProperty("data").EnumerateArray().First();
        first.TryGetProperty("quizId", out _).Should().BeTrue();
        first.TryGetProperty("title", out _).Should().BeTrue();
        first.TryGetProperty("passScore", out _).Should().BeTrue();
        first.GetProperty("isActive").GetBoolean().Should().BeTrue(
            because: "student endpoint must only return active quizzes");
    }

    [Fact(DisplayName = "BE-IT-SQ-07 — GET /api/student/quizzes/1: returns 200 with active quiz")]
    public async Task GetActiveQuizById_ExistingId_Returns200()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/1");
        await AssertSuccessAsync(response, HttpStatusCode.OK);

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").GetProperty("quizId").GetInt32().Should().Be(1);
    }

    [Fact(DisplayName = "BE-IT-SQ-08 — GET /api/student/quizzes/999: returns 404 when quiz inactive or missing")]
    public async Task GetActiveQuizById_MissingOrInactiveId_Returns404()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-SQ-09 — GET /api/student/quizzes/1: no token returns 401")]
    public async Task GetActiveQuizById_NoToken_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/student/quizzes/1");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-SQ-10 — GET /api/student/quizzes/1: response is active quiz (isActive = true)")]
    public async Task GetActiveQuizById_ResponseIsActive()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/1");
        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").GetProperty("isActive").GetBoolean()
           .Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SQ-11 — GET /api/student/quizzes/1/questions: returns 200 with question list")]
    public async Task GetActiveQuestions_ExistingQuiz_Returns200()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/1/questions");
        await AssertSuccessAsync(response, HttpStatusCode.OK);

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "BE-IT-SQ-12 — GET /api/student/quizzes/1/questions: response MUST NOT include correctOption")]
    public async Task GetActiveQuestions_ResponseMustNotIncludeCorrectOption()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/1/questions");
        using var doc = await ParseAsync(response);

        var first = doc.RootElement.GetProperty("data").EnumerateArray().First();

        // Security contract: correct answer must never appear before submission
        first.TryGetProperty("correctOption", out _).Should().BeFalse(
            because: "sending the correct answer before submission would allow cheating");
    }

    [Fact(DisplayName = "BE-IT-SQ-13 — GET /api/student/quizzes/1/questions: response MUST NOT include explanation")]
    public async Task GetActiveQuestions_ResponseMustNotIncludeExplanation()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/1/questions");
        using var doc = await ParseAsync(response);

        var first = doc.RootElement.GetProperty("data").EnumerateArray().First();

        first.TryGetProperty("explanation", out _).Should().BeFalse(
            because: "explanation is only revealed after the student submits answers");
    }

    [Fact(DisplayName = "BE-IT-SQ-14 — GET /api/student/quizzes/999/questions: returns 404 when quiz inactive or missing")]
    public async Task GetActiveQuestions_MissingQuiz_Returns404()
    {
        var response = await _studentClient.GetAsync("/api/student/quizzes/999/questions");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-SQ-15 — POST /api/student/quizzes/1/attempt: valid submission returns 200 OK")]
    public async Task SubmitAttempt_ValidSubmission_Returns200()
    {
        var response = await _studentClient.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "A" } } });

        await AssertSuccessAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-SQ-16 — POST /api/student/quizzes/1/attempt: response includes grading fields")]
    public async Task SubmitAttempt_ValidSubmission_ResponseIncludesGradingFields()
    {
        var response = await _studentClient.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "B" } } });

        using var doc = await ParseAsync(response);
        var data = doc.RootElement.GetProperty("data");

        data.TryGetProperty("attemptId", out _).Should().BeTrue();
        data.TryGetProperty("score", out _).Should().BeTrue();
        data.TryGetProperty("correctCount", out _).Should().BeTrue();
        data.TryGetProperty("totalQuestions", out _).Should().BeTrue();
        data.TryGetProperty("xpEarned", out _).Should().BeTrue();
        data.TryGetProperty("passed", out _).Should().BeTrue();
        data.TryGetProperty("isFirstAttempt", out _).Should().BeTrue();
        data.TryGetProperty("results", out var results).Should().BeTrue();
        results.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "BE-IT-SQ-17 — POST /api/student/quizzes/1/attempt: per-question results include correctOption and isCorrect")]
    public async Task SubmitAttempt_ValidSubmission_ResultsIncludePerQuestionFeedback()
    {
        var response = await _studentClient.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "A" } } });

        using var doc = await ParseAsync(response);
        var firstResult = doc.RootElement.GetProperty("data")
            .GetProperty("results").EnumerateArray().First();

        firstResult.TryGetProperty("questionId", out _).Should().BeTrue();
        firstResult.TryGetProperty("selectedOption", out _).Should().BeTrue();
        firstResult.TryGetProperty("correctOption", out _).Should().BeTrue(
            because: "correctOption is revealed POST-submission as part of feedback");
        firstResult.TryGetProperty("isCorrect", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SQ-18 — POST /api/student/quizzes/1/attempt: Admin token is accepted")]
    public async Task SubmitAttempt_AdminToken_Returns200()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "C" } } });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-SQ-19 — POST /api/student/quizzes/1/attempt: no token returns 401")]
    public async Task SubmitAttempt_NoToken_Returns401()
    {
        var response = await _anonClient.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "A" } } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-SQ-20 — POST /api/student/quizzes/1/attempt: empty answers list returns 400 Validation Failed")]
    public async Task SubmitAttempt_EmptyAnswersList_Returns400()
    {
        // MinLength(1) on Answers — must be rejected before reaching service
        var response = await _studentClient.PostAsync(
            "/api/student/quizzes/1/attempt",
            Json("""{ "answers": [] }"""));

        await AssertValidationErrorAsync(response, "Answers");
    }

    [Fact(DisplayName = "BE-IT-SQ-21 — POST /api/student/quizzes/1/attempt: missing answers field returns 400")]
    public async Task SubmitAttempt_MissingAnswers_Returns400()
    {
        var response = await _studentClient.PostAsync(
            "/api/student/quizzes/1/attempt",
            Json("{}"));

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "BE-IT-SQ-22 — POST /api/student/quizzes/1/attempt: invalid selectedOption ('E') returns 400")]
    public async Task SubmitAttempt_InvalidSelectedOption_Returns400()
    {
        var response = await _studentClient.PostAsync(
            "/api/student/quizzes/1/attempt",
            Json("""{ "answers": [{ "questionId": 1, "selectedOption": "E" }] }"""));

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "BE-IT-SQ-23 — POST /api/student/quizzes/1/attempt: questionId = 0 returns 400 (must be > 0)")]
    public async Task SubmitAttempt_QuestionIdZero_Returns400()
    {
        var response = await _studentClient.PostAsync(
            "/api/student/quizzes/1/attempt",
            Json("""{ "answers": [{ "questionId": 0, "selectedOption": "A" }] }"""));

        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "BE-IT-SQ-24 — POST /api/student/quizzes/999/attempt: returns 404 when quiz not found")]
    public async Task SubmitAttempt_MissingQuiz_Returns404()
    {
        var response = await _studentClient.PostAsJsonAsync(
            "/api/student/quizzes/999/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "A" } } });

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-SQ-25 — POST /api/student/quizzes/1/attempt: service ArgumentException (wrong answer count) returns 400")]
    public async Task SubmitAttempt_ServiceArgumentException_Returns400()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IQuizAttemptService>();
                services.AddScoped<IQuizAttemptService, InvalidAnswersAttemptService>();
            });
        });

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.UserToken);

        var response = await client.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "A" } } });

        // ArgumentException from service → controller converts to 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString()
           .Should().Contain("3 answers");
    }

    [Fact(DisplayName = "BE-IT-SQ-26 — POST /api/student/quizzes/1/attempt: malformed JSON returns 400")]
    public async Task SubmitAttempt_MalformedJson_Returns400()
    {
        var response = await _studentClient.PostAsync(
            "/api/student/quizzes/1/attempt",
            Json("{ this is not valid json }"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "malformed JSON must be rejected by the model binder");
    }

    [Fact(DisplayName = "BE-IT-SQ-27 — POST /api/student/quizzes/1/attempt: response message confirms submission")]
    public async Task SubmitAttempt_ValidSubmission_ResponseMessageConfirmsSubmission()
    {
        var response = await _studentClient.PostAsJsonAsync(
            "/api/student/quizzes/1/attempt",
            new { answers = new[] { new { questionId = 1, selectedOption = "A" } } });

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("message").GetString()
           .Should().Contain("submitted");
    }
}
