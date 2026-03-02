using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.Validation;

/// <summary>
/// S1-US5 — Input Validation Strategy (Integration-testing tier).
///
/// Sends real HTTP requests through the full ASP.NET Core pipeline
/// (model binding → DataAnnotations → InvalidModelStateResponseFactory →
/// controller → middleware) and asserts that the API returns the expected
/// HTTP 400 Bad Request with the standardised error envelope whenever the
/// input is invalid.
///
/// Actual 400 shape (configured in Program.cs):
///   { statusCode: 400, message: "Validation Failed", errors: { Field: ["msg"] } }
///
/// All requests are authenticated as Admin so that any 400 that appears is
/// caused exclusively by input-validation failures — NOT by auth/authz.
/// </summary>
public class InputValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InputValidationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // All requests authenticated as Admin so authz never blocks us
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// Parses the response body and returns the root JSON document.
    /// Fails the test immediately if the body is not valid JSON.
    /// </summary>
    private static async Task<JsonDocument> ParseBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace(because: "API must always return a JSON body");
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Asserts that the response is 400 and that the body matches the
    /// standardised validation-error envelope produced by
    /// <c>InvalidModelStateResponseFactory</c> in Program.cs:
    ///   { statusCode: 400, message: "Validation Failed", errors: { ... } }
    /// </summary>
    private static async Task AssertValidationBadRequestAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "invalid input must be rejected with HTTP 400");

        using var doc = await ParseBodyAsync(response);
        var root = doc.RootElement;

        root.TryGetProperty("statusCode", out var statusCodeProp).Should().BeTrue(
            because: "error body must contain 'statusCode' field");
        statusCodeProp.GetInt32().Should().Be(400);

        root.TryGetProperty("message", out var messageProp).Should().BeTrue(
            because: "error body must contain 'message' field");
        messageProp.GetString().Should().Be("Validation Failed");

        root.TryGetProperty("errors", out _).Should().BeTrue(
            because: "validation error body must contain an 'errors' field");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  POST /api/users — CreateUserDto
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-IV-01 — POST /api/users: empty body returns 400 Validation Failed")]
    public async Task CreateUser_EmptyBody_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/users",
            Json("{}"));

        await AssertValidationBadRequestAsync(response);
    }

    [Fact(DisplayName = "TC-IV-02 — POST /api/users: missing email field returns 400")]
    public async Task CreateUser_MissingEmail_Returns400()
    {
        var payload = new { username = "alice", role = "Student" };
        var response = await _client.PostAsJsonAsync("/api/users", payload);

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement
           .GetProperty("errors")
           .TryGetProperty("Email", out _)
           .Should().BeTrue(because: "errors map must include the 'Email' key");
    }

    [Fact(DisplayName = "TC-IV-03 — POST /api/users: invalid email format returns 400")]
    public async Task CreateUser_InvalidEmailFormat_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/users",
            Json("""{ "email": "not-an-email", "username": "alice", "role": "Student" }"""));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errorsEl = doc.RootElement.GetProperty("errors");
        errorsEl.TryGetProperty("Email", out var emailErrors).Should().BeTrue();
        emailErrors.EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("A valid email address is required.");
    }

    [Fact(DisplayName = "TC-IV-04 — POST /api/users: missing username returns 400")]
    public async Task CreateUser_MissingUsername_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/users",
            Json("""{ "email": "alice@example.com", "role": "Student" }"""));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement
           .GetProperty("errors")
           .TryGetProperty("Username", out _)
           .Should().BeTrue(because: "errors map must include the 'Username' key");
    }

    [Fact(DisplayName = "TC-IV-05 — POST /api/users: username too short (1 char) returns 400")]
    public async Task CreateUser_UsernameTooShort_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/users",
            Json("""{ "email": "alice@example.com", "username": "a", "role": "Student" }"""));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errorsEl = doc.RootElement.GetProperty("errors");
        errorsEl.TryGetProperty("Username", out var usernameErrors).Should().BeTrue();
        usernameErrors.EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("Username must be between 2 and 100 characters.");
    }

    [Fact(DisplayName = "TC-IV-06 — POST /api/users: invalid role value returns 400")]
    public async Task CreateUser_InvalidRole_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/users",
            Json("""{ "email": "alice@example.com", "username": "alice", "role": "Manager" }"""));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errorsEl = doc.RootElement.GetProperty("errors");
        errorsEl.TryGetProperty("Role", out var roleErrors).Should().BeTrue();
        roleErrors.EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("Role must be 'Student', 'Admin', or 'Instructor'.");
    }

    [Fact(DisplayName = "TC-IV-07 — POST /api/users: omitted role uses default 'Student' and passes validation")]
    public async Task CreateUser_OmittedRole_PassesValidation()
    {
        // Role defaults to "Student" in CreateUserDto — a valid value.
        // Validation must NOT reject this request; it should reach the service layer.
        var response = await _client.PostAsync(
            "/api/users",
            Json("""{ "email": "alice@example.com", "username": "alice" }"""));

        // The request passes model validation.  The stub service returns null
        // (simulating a duplicate-email business error) which makes the controller
        // respond with 400 carrying a "duplicate email" message — NOT "Validation Failed".
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var doc = await ParseBodyAsync(response);
            doc.RootElement
               .GetProperty("message")
               .GetString()
               .Should().NotBe("Validation Failed",
                   because: "the default role 'Student' is valid and must not trigger model validation errors");
        }
    }

    [Fact(DisplayName = "TC-IV-08 — POST /api/users: completely malformed JSON returns 400")]
    public async Task CreateUser_MalformedJson_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/users",
            Json("{ this is not valid json }"));

        // ASP.NET's model-binding layer rejects malformed JSON with 400
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "the framework must reject unparseable request bodies");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PUT /api/users/{id} — UpdateUserDto
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-IV-09 — PUT /api/users/1: empty body returns 400 Validation Failed")]
    public async Task UpdateUser_EmptyBody_Returns400()
    {
        var response = await _client.PutAsync(
            "/api/users/1",
            Json("{}"));

        await AssertValidationBadRequestAsync(response);
    }

    [Fact(DisplayName = "TC-IV-10 — PUT /api/users/1: invalid role value returns 400")]
    public async Task UpdateUser_InvalidRole_Returns400()
    {
        var response = await _client.PutAsync(
            "/api/users/1",
            Json("""{ "role": "Viewer", "isActive": true }"""));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errorsEl = doc.RootElement.GetProperty("errors");
        errorsEl.TryGetProperty("Role", out var roleErrors).Should().BeTrue();
        roleErrors.EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain("Role must be 'Student', 'Admin', or 'Instructor'.");
    }

    [Fact(DisplayName = "TC-IV-11 — PUT /api/users/1: missing isActive field returns 400")]
    public async Task UpdateUser_MissingIsActive_Returns400()
    {
        var response = await _client.PutAsync(
            "/api/users/1",
            Json("""{ "role": "Admin" }"""));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement
           .GetProperty("errors")
           .TryGetProperty("IsActive", out _)
           .Should().BeTrue(because: "errors map must include the 'IsActive' key");
    }

    [Fact(DisplayName = "TC-IV-12 — PUT /api/users/1: valid body passes validation (any non-400 from validation)")]
    public async Task UpdateUser_ValidBody_PassesValidation()
    {
        var response = await _client.PutAsync(
            "/api/users/1",
            Json("""{ "role": "Instructor", "isActive": false }"""));

        // Model validation passed if the response is NOT a 400 caused by
        // "Validation failed." (the service stub may return null → controller
        // may return 200 with null data, which is acceptable for this test).
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var doc = await ParseBodyAsync(response);
            doc.RootElement
               .GetProperty("message")
               .GetString()
               .Should().NotBe("Validation Failed",
                   because: "a valid body must not trigger model-validation errors");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Response-shape contract
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-IV-13 — Validation error response always contains statusCode/message/errors")]
    public async Task ValidationError_ResponseShape_ContainsRequiredKeys()
    {
        // Trigger a guaranteed validation error (bad email, short username, invalid role)
        var response = await _client.PostAsync(
            "/api/users",
            Json("""{ "email": "bad", "username": "x", "role": "Unknown" }"""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var doc = await ParseBodyAsync(response);
        var root = doc.RootElement;

        // All three envelope keys must be present
        root.TryGetProperty("statusCode", out var statusCodeEl).Should().BeTrue();
        statusCodeEl.GetInt32().Should().Be(400);

        root.TryGetProperty("message", out var messageEl).Should().BeTrue();
        messageEl.GetString().Should().Be("Validation Failed");

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();

        // errors must be a non-empty object (dictionary), not null / array
        errorsEl.ValueKind.Should().Be(JsonValueKind.Object,
            because: "errors must be a key→messages dictionary");
        errorsEl.EnumerateObject().Should().NotBeEmpty(
            because: "at least one field must have failed validation");
    }
}
