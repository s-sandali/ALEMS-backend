using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.CodeExecution;

/// <summary>
/// Integration tests for the Judge0-backed code-execution endpoints.
/// These tests run through the full ASP.NET Core pipeline while using the
/// deterministic test auth handler, so the only external dependency is the
/// local Judge0 instance configured in appsettings.json.
/// </summary>
public class CodeExecutionEndpointIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CodeExecutionEndpointIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.UserToken);
    }

    [Fact(DisplayName = "TC-CE-01 - GET /api/code/languages returns supported local language list")]
    public async Task GetLanguages_ReturnsSupportedLanguages()
    {
        var response = await _client.GetAsync("/api/code/languages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");

        var languages = root.GetProperty("data").EnumerateArray().ToList();
        languages.Should().NotBeEmpty();
        languages.Should().Contain(l =>
            l.GetProperty("languageId").GetInt32() == 71 &&
            l.GetProperty("name").GetString() == "Python 3");
    }

    [Fact(DisplayName = "TC-CE-02 - POST /api/code/execute returns Judge0 execution result")]
    public async Task Execute_WithPythonProgram_ReturnsAcceptedResult()
    {
        var payload = new
        {
            sourceCode = "print('hello world')",
            languageId = 71,
            stdin = ""
        };

        var response = await _client.PostAsJsonAsync("/api/code/execute", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");

        var data = root.GetProperty("data");
        data.GetProperty("statusId").GetInt32().Should().Be(3);
        data.GetProperty("statusDescription").GetString().Should().Be("Accepted");
        data.GetProperty("stdout").GetString().Should().Be("hello world\n");
        data.TryGetProperty("executionTime", out _).Should().BeTrue();
        data.TryGetProperty("memoryUsed", out _).Should().BeTrue();
    }
}
