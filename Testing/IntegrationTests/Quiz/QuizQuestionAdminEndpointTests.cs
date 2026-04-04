using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.Quiz;

/// <summary>
/// Integration tests for the Admin quiz-question management API.
///
///   /api/quizzes/{quizId}/questions
///   /api/quizzes/{quizId}/questions/{id}
///
/// Validates routing, authorization, model-binding, validation,
/// controller ownership guards, and response envelope shape.
///
/// Test groups
/// ───────────
/// BE-IT-QQ-01..03   Authorization enforcement
/// BE-IT-QQ-04..07   GET /api/quizzes/{quizId}/questions
/// BE-IT-QQ-08..11   GET /api/quizzes/{quizId}/questions/{id}
/// BE-IT-QQ-12..22   POST /api/quizzes/{quizId}/questions  (create + validation)
/// BE-IT-QQ-23..27   PUT  /api/quizzes/{quizId}/questions/{id}
/// BE-IT-QQ-28..31   DELETE /api/quizzes/{quizId}/questions/{id}
/// </summary>
public class QuizQuestionAdminEndpointTests : IClassFixture<QuizWebApplicationFactory>
{
    private readonly HttpClient _adminClient;
    private readonly HttpClient _studentClient;
    private readonly HttpClient _anonClient;

    public QuizQuestionAdminEndpointTests(QuizWebApplicationFactory factory)
    {
        _adminClient = factory.CreateClient();
        _adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        _studentClient = factory.CreateClient();
        _studentClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.UserToken);

        _anonClient = factory.CreateClient();
    }

    // —— Helpers ————————————————————————————————————————————————————————————————

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage r)
    {
        var body = await r.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
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

    /// <summary>Builds a fully-valid CreateQuizQuestionDto payload object.</summary>
    private static object ValidQuestionPayload(
        string questionType  = "MCQ",
        string questionText  = "Which option is correct?",
        string correctOption = "A",
        string difficulty    = "easy") => new
    {
        questionType,
        questionText,
        optionA       = "Option A",
        optionB       = "Option B",
        optionC       = "Option C",
        optionD       = "Option D",
        correctOption,
        difficulty,
        orderIndex    = 0
    };

    [Fact(DisplayName = "BE-IT-QQ-01 — GET /api/quizzes/1/questions: no token returns 401")]
    public async Task GetList_NoToken_Returns401()
    {
        var response = await _anonClient.GetAsync("/api/quizzes/1/questions");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-QQ-02 — GET /api/quizzes/1/questions: Student token returns 403 (Admin-only)")]
    public async Task GetList_StudentToken_Returns403()
    {
        var response = await _studentClient.GetAsync("/api/quizzes/1/questions");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-QQ-03 — POST /api/quizzes/1/questions: Student token returns 403")]
    public async Task Create_StudentToken_Returns403()
    {
        var response = await _studentClient.PostAsJsonAsync(
            "/api/quizzes/1/questions", ValidQuestionPayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-QQ-04 — GET /api/quizzes/1/questions: returns 200 with question list")]
    public async Task GetList_ExistingQuiz_Returns200WithQuestions()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1/questions");
        await AssertSuccessAsync(response, HttpStatusCode.OK);

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").ValueKind
           .Should().Be(JsonValueKind.Array);
    }

    [Fact(DisplayName = "BE-IT-QQ-05 — GET /api/quizzes/1/questions: question objects include CorrectOption (admin-only field)")]
    public async Task GetList_ExistingQuiz_IncludesCorrectOptionForAdmin()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1/questions");

        using var doc = await ParseAsync(response);
        var first = doc.RootElement.GetProperty("data").EnumerateArray().First();

        // Admin DTO must expose correctOption
        first.TryGetProperty("correctOption", out var correctOpt).Should().BeTrue(
            because: "admin API must return the correct answer");
        correctOpt.GetString().Should().BeOneOf("A", "B", "C", "D");
    }

    [Fact(DisplayName = "BE-IT-QQ-06 — GET /api/quizzes/999/questions: returns 404 when quiz not found")]
    public async Task GetList_MissingQuiz_Returns404()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/999/questions");
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-QQ-07 — GET /api/quizzes/1/questions: question has required structural fields")]
    public async Task GetList_ExistingQuiz_QuestionHasRequiredFields()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1/questions");

        using var doc = await ParseAsync(response);
        var first = doc.RootElement.GetProperty("data").EnumerateArray().First();

        first.TryGetProperty("questionId", out _).Should().BeTrue();
        first.TryGetProperty("quizId", out _).Should().BeTrue();
        first.TryGetProperty("questionType", out _).Should().BeTrue();
        first.TryGetProperty("questionText", out _).Should().BeTrue();
        first.TryGetProperty("difficulty", out _).Should().BeTrue();
        first.TryGetProperty("isActive", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-QQ-08 — GET /api/quizzes/1/questions/1: returns 200 OK")]
    public async Task GetById_MatchingQuizAndQuestion_Returns200()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1/questions/1");
        await AssertSuccessAsync(response, HttpStatusCode.OK);

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data").GetProperty("questionId").GetInt32()
           .Should().Be(1);
    }

    [Fact(DisplayName = "BE-IT-QQ-09 — GET /api/quizzes/1/questions/999: returns 404 when question not found")]
    public async Task GetById_MissingQuestion_Returns404()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1/questions/999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "BE-IT-QQ-10 — GET /api/quizzes/2/questions/1: returns 404 when question belongs to different quiz")]
    public async Task GetById_QuestionBelongsToDifferentQuiz_Returns404()
    {
        // Question 1 belongs to quizId=1; requesting via quizId=2 must fail
        var response = await _adminClient.GetAsync("/api/quizzes/2/questions/1");
        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "BE-IT-QQ-11 — GET /api/quizzes/1/questions/1: response includes correctOption field")]
    public async Task GetById_ExistingQuestion_IncludesCorrectOption()
    {
        var response = await _adminClient.GetAsync("/api/quizzes/1/questions/1");

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data")
           .TryGetProperty("correctOption", out var co).Should().BeTrue();
        co.GetString().Should().BeOneOf("A", "B", "C", "D");
    }

    [Fact(DisplayName = "BE-IT-QQ-12 — POST /api/quizzes/1/questions: valid MCQ payload returns 201")]
    public async Task Create_ValidMcqPayload_Returns201()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/quizzes/1/questions", ValidQuestionPayload());

        await AssertSuccessAsync(response, HttpStatusCode.Created);
    }

    [Fact(DisplayName = "BE-IT-QQ-13 — POST /api/quizzes/1/questions: valid PREDICT_STEP payload returns 201")]
    public async Task Create_ValidPredictStepPayload_Returns201()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/quizzes/1/questions",
            ValidQuestionPayload(questionType: "PREDICT_STEP"));

        await AssertSuccessAsync(response, HttpStatusCode.Created);
    }

    [Fact(DisplayName = "BE-IT-QQ-14 — POST /api/quizzes/1/questions: response includes new questionId")]
    public async Task Create_ValidPayload_ResponseIncludesQuestionId()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/quizzes/1/questions", ValidQuestionPayload());

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data")
           .TryGetProperty("questionId", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-QQ-15 — POST /api/quizzes/999/questions: returns 404 when quiz not found")]
    public async Task Create_MissingQuiz_Returns404()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/quizzes/999/questions", ValidQuestionPayload());

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "999");
    }

    [Fact(DisplayName = "BE-IT-QQ-16 — POST /api/quizzes/1/questions: missing questionText returns 400")]
    public async Task Create_MissingQuestionText_Returns400()
    {
        var response = await _adminClient.PostAsync(
            "/api/quizzes/1/questions",
            Json("""{ "questionType":"MCQ","optionA":"A","optionB":"B","optionC":"C","optionD":"D","correctOption":"A","difficulty":"easy" }"""));

        await AssertValidationErrorAsync(response, "QuestionText");
    }

    [Fact(DisplayName = "BE-IT-QQ-17 — POST /api/quizzes/1/questions: invalid correctOption ('E') returns 400")]
    public async Task Create_InvalidCorrectOption_Returns400()
    {
        var response = await _adminClient.PostAsync(
            "/api/quizzes/1/questions",
            Json("""{ "questionType":"MCQ","questionText":"Valid question text","optionA":"A","optionB":"B","optionC":"C","optionD":"D","correctOption":"E","difficulty":"easy" }"""));

        await AssertValidationErrorAsync(response, "CorrectOption");
    }

    [Fact(DisplayName = "BE-IT-QQ-18 — POST /api/quizzes/1/questions: invalid difficulty ('extreme') returns 400")]
    public async Task Create_InvalidDifficulty_Returns400()
    {
        var response = await _adminClient.PostAsync(
            "/api/quizzes/1/questions",
            Json("""{ "questionType":"MCQ","questionText":"Valid question text","optionA":"A","optionB":"B","optionC":"C","optionD":"D","correctOption":"A","difficulty":"extreme" }"""));

        await AssertValidationErrorAsync(response, "Difficulty");
    }

    [Fact(DisplayName = "BE-IT-QQ-19 — POST /api/quizzes/1/questions: invalid questionType ('OPEN') returns 400")]
    public async Task Create_InvalidQuestionType_Returns400()
    {
        var response = await _adminClient.PostAsync(
            "/api/quizzes/1/questions",
            Json("""{ "questionType":"OPEN","questionText":"Valid question text","optionA":"A","optionB":"B","optionC":"C","optionD":"D","correctOption":"A","difficulty":"easy" }"""));

        await AssertValidationErrorAsync(response, "QuestionType");
    }

    [Fact(DisplayName = "BE-IT-QQ-20 — POST /api/quizzes/1/questions: empty body returns 400")]
    public async Task Create_EmptyBody_Returns400()
    {
        var response = await _adminClient.PostAsync("/api/quizzes/1/questions", Json("{}"));
        await AssertValidationErrorAsync(response);
    }

    [Fact(DisplayName = "BE-IT-QQ-21 — POST /api/quizzes/1/questions: questionText too short (4 chars) returns 400")]
    public async Task Create_QuestionTextTooShort_Returns400()
    {
        var response = await _adminClient.PostAsync(
            "/api/quizzes/1/questions",
            Json("""{ "questionType":"MCQ","questionText":"Hi?","optionA":"A","optionB":"B","optionC":"C","optionD":"D","correctOption":"A","difficulty":"easy" }"""));

        await AssertValidationErrorAsync(response, "QuestionText");
    }

    [Fact(DisplayName = "BE-IT-QQ-22 — POST /api/quizzes/1/questions: hard difficulty is a valid value")]
    public async Task Create_HardDifficulty_Returns201()
    {
        var response = await _adminClient.PostAsJsonAsync(
            "/api/quizzes/1/questions",
            ValidQuestionPayload(difficulty: "hard"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private static object ValidUpdatePayload() => new
    {
        questionType  = "MCQ",
        questionText  = "Updated question text here",
        optionA       = "A",
        optionB       = "B",
        optionC       = "C",
        optionD       = "D",
        correctOption = "B",
        difficulty    = "medium",
        isActive      = true,
        orderIndex    = 1
    };

    [Fact(DisplayName = "BE-IT-QQ-23 — PUT /api/quizzes/1/questions/1: valid payload returns 200 OK")]
    public async Task Update_ValidPayload_Returns200()
    {
        var response = await _adminClient.PutAsJsonAsync(
            "/api/quizzes/1/questions/1", ValidUpdatePayload());

        await AssertSuccessAsync(response, HttpStatusCode.OK);
    }

    [Fact(DisplayName = "BE-IT-QQ-24 — PUT /api/quizzes/1/questions/1: response contains updated question text")]
    public async Task Update_ValidPayload_ResponseReflectsUpdate()
    {
        var response = await _adminClient.PutAsJsonAsync(
            "/api/quizzes/1/questions/1", ValidUpdatePayload());

        using var doc = await ParseAsync(response);
        doc.RootElement.GetProperty("data")
           .GetProperty("questionText").GetString()
           .Should().NotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("message").GetString()
           .Should().Contain("updated");
    }

    [Fact(DisplayName = "BE-IT-QQ-25 — PUT /api/quizzes/2/questions/1: returns 404 (question belongs to quiz 1, not quiz 2)")]
    public async Task Update_QuestionInDifferentQuiz_Returns404()
    {
        // Ownership guard: question 1 belongs to quiz 1; updating via quiz 2 must fail
        var response = await _adminClient.PutAsJsonAsync(
            "/api/quizzes/2/questions/1", ValidUpdatePayload());

        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "BE-IT-QQ-26 — PUT /api/quizzes/1/questions/1: missing questionText returns 400")]
    public async Task Update_MissingQuestionText_Returns400()
    {
        var response = await _adminClient.PutAsync(
            "/api/quizzes/1/questions/1",
            Json("""{ "questionType":"MCQ","optionA":"A","optionB":"B","optionC":"C","optionD":"D","correctOption":"A","difficulty":"easy","isActive":true }"""));

        await AssertValidationErrorAsync(response, "QuestionText");
    }

    [Fact(DisplayName = "BE-IT-QQ-27 — PUT /api/quizzes/1/questions/999: returns 404 when question does not exist")]
    public async Task Update_MissingQuestion_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync(
            "/api/quizzes/1/questions/999", ValidUpdatePayload());

        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "BE-IT-QQ-28 — DELETE /api/quizzes/1/questions/1: returns 204 No Content")]
    public async Task Delete_ExistingQuestion_Returns204()
    {
        var response = await _adminClient.DeleteAsync("/api/quizzes/1/questions/1");
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().BeNullOrEmpty(because: "204 response must have no body");
    }

    [Fact(DisplayName = "BE-IT-QQ-29 — DELETE /api/quizzes/2/questions/1: returns 404 (ownership guard)")]
    public async Task Delete_QuestionInDifferentQuiz_Returns404()
    {
        var response = await _adminClient.DeleteAsync("/api/quizzes/2/questions/1");
        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "BE-IT-QQ-30 — DELETE /api/quizzes/1/questions/999: returns 404 when question missing")]
    public async Task Delete_MissingQuestion_Returns404()
    {
        var response = await _adminClient.DeleteAsync("/api/quizzes/1/questions/999");
        await AssertErrorAsync(response, HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "BE-IT-QQ-31 — DELETE /api/quizzes/1/questions/1: Student token returns 403")]
    public async Task Delete_StudentToken_Returns403()
    {
        var response = await _studentClient.DeleteAsync("/api/quizzes/1/questions/1");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
