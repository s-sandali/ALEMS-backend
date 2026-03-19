using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.Simulation;

public class SimulationEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SimulationEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);
    }

    [Fact(DisplayName = "BE-IT-SIM-01 - POST /api/simulation/run returns full trace for valid unsorted input")]
    public async Task Run_WithValidUnsortedInput_ReturnsFullBubbleSortTrace()
    {
        var payload = new { algorithm = "bubble_sort", array = new[] { 5, 3, 4, 1 } };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("algorithmName").GetString().Should().Be("Bubble Sort");
        var steps = root.GetProperty("steps");
        root.GetProperty("totalSteps").GetInt32().Should().Be(steps.GetArrayLength());

        var actionLabels = steps.EnumerateArray()
            .Select(step => step.GetProperty("actionLabel").GetString())
            .ToList();

        actionLabels.Should().Contain("compare");
        actionLabels.Should().Contain("swap");
        actionLabels.Last().Should().Be("complete");
    }

    [Theory(DisplayName = "BE-IT-SIM-02 - POST /api/simulation/run accepts normalized algorithm keys")]
    [InlineData("bubble_sort")]
    [InlineData("bubble-sort")]
    [InlineData("  BUBBLE_SORT  ")]
    public async Task Run_WithNormalizedAlgorithmKeys_ResolvesSuccessfully(string algorithm)
    {
        var payload = new { algorithm, array = new[] { 3, 2, 1 } };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("algorithmName").GetString().Should().Be("Bubble Sort");
    }

    [Fact(DisplayName = "BE-IT-SIM-03 - POST /api/simulation/run returns 501 for unsupported algorithm")]
    public async Task Run_WithUnsupportedAlgorithm_Returns501WithErrorBody()
    {
        var payload = new { algorithm = "quick_sort", array = new[] { 3, 2, 1 } };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("not supported");
    }

    [Theory(DisplayName = "BE-IT-SIM-04 - POST /api/simulation/run validates empty algorithm or empty array")]
    [InlineData("", new[] { 3, 2, 1 })]
    [InlineData("bubble_sort", new int[0])]
    public async Task Run_WithInvalidInput_Returns400ValidationBody(string algorithm, int[] array)
    {
        var payload = new { algorithm, array };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SIM-05 - POST /api/simulation/start creates practice session")]
    public async Task Start_WithValidInput_CreatesSessionWithActionableCurrentStep()
    {
        var payload = new { algorithm = "bubble_sort", array = new[] { 5, 3, 4, 1 } };

        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("sessionId").GetString().Should().NotBeNullOrWhiteSpace();

        var steps = root.GetProperty("steps");
        steps.GetArrayLength().Should().BeGreaterThan(0);

        var currentStepIndex = root.GetProperty("currentStepIndex").GetInt32();
        currentStepIndex.Should().BeGreaterThanOrEqualTo(0);
        currentStepIndex.Should().BeLessThan(steps.GetArrayLength());

        var firstAction = steps[currentStepIndex].GetProperty("actionLabel").GetString();
        firstAction.Should().Be("swap");
    }

    [Fact(DisplayName = "BE-IT-SIM-06 - POST /api/simulation/start handles already sorted input")]
    public async Task Start_WithSortedInput_StartsAtTerminalOrEarlyExitActionableStep()
    {
        var payload = new { algorithm = "bubble_sort", array = new[] { 1, 2, 3 } };

        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var steps = root.GetProperty("steps");
        var currentStepIndex = root.GetProperty("currentStepIndex").GetInt32();
        var actionLabel = steps[currentStepIndex].GetProperty("actionLabel").GetString();

        actionLabel.Should().BeOneOf("early_exit", "complete");
    }

    [Fact(DisplayName = "BE-IT-SIM-07 - POST /api/simulation/start returns 501 for unsupported algorithm")]
    public async Task Start_WithUnsupportedAlgorithm_Returns501WithErrorBody()
    {
        var payload = new { algorithm = "merge_sort", array = new[] { 3, 2, 1 } };

        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not supported");
    }

    [Theory(DisplayName = "BE-IT-SIM-08 - POST /api/simulation/start validates empty algorithm or empty array")]
    [InlineData("", new[] { 3, 2, 1 })]
    [InlineData("bubble_sort", new int[0])]
    public async Task Start_WithInvalidInput_Returns400ValidationBody(string algorithm, int[] array)
    {
        var payload = new { algorithm, array };

        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SIM-09 - POST /api/simulation/validate-step accepts correct swap and advances")]
    public async Task ValidateStep_WithCorrectSwap_AdvancesSession()
    {
        var sessionId = await StartSessionAsync(new[] { 5, 3, 4, 1 });

        var payload = new
        {
            sessionId,
            action = new { type = "swap", indices = new[] { 0, 1 } }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("correct").GetBoolean().Should().BeTrue();
        root.GetProperty("newArrayState").EnumerateArray().Select(x => x.GetInt32()).Should().Equal(3, 5, 4, 1);
        root.GetProperty("nextExpectedAction").GetString().Should().Be("swap");
        root.GetProperty("currentStepIndex").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "BE-IT-SIM-10 - POST /api/simulation/validate-step rejects incorrect swap without advancing")]
    public async Task ValidateStep_WithIncorrectSwap_DoesNotAdvanceSession()
    {
        var startResponse = await StartSessionResponseAsync(new[] { 5, 3, 4, 1 });
        var sessionId = startResponse.RootElement.GetProperty("sessionId").GetString()!;
        var initialIndex = startResponse.RootElement.GetProperty("currentStepIndex").GetInt32();

        var payload = new
        {
            sessionId,
            action = new { type = "swap", indices = new[] { 1, 2 } }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("correct").GetBoolean().Should().BeFalse();
        root.GetProperty("newArrayState").EnumerateArray().Select(x => x.GetInt32()).Should().Equal(5, 3, 4, 1);
        root.GetProperty("hint").GetString().Should().NotBeNullOrWhiteSpace();
        root.GetProperty("currentStepIndex").GetInt32().Should().Be(initialIndex);
    }

    [Fact(DisplayName = "BE-IT-SIM-11 - POST /api/simulation/validate-step returns practice-complete at terminal state")]
    public async Task ValidateStep_WhenSessionAtTerminalState_ReturnsCompleteSemantics()
    {
        var sessionId = await StartSessionAsync(new[] { 1, 2, 3 });

        var payload = new
        {
            sessionId,
            action = new { type = "swap", indices = new[] { 0, 1 } }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.GetProperty("correct").GetBoolean().Should().BeFalse();
        root.GetProperty("nextExpectedAction").GetString().Should().Be("complete");
        root.GetProperty("message").GetString().Should().Contain("Practice complete");
    }

    [Fact(DisplayName = "BE-IT-SIM-12 - POST /api/simulation/validate-step accepts legacy userAction alias")]
    public async Task ValidateStep_WithLegacyUserActionAlias_BindsAndWorks()
    {
        var sessionId = await StartSessionAsync(new[] { 5, 3, 4, 1 });

        var payload = new
        {
            sessionId,
            userAction = new { type = "swap", indices = new[] { 0, 1 } }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("correct").GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SIM-13 - POST /api/simulation/validate-step returns 404 for unknown session id")]
    public async Task ValidateStep_WithUnknownSessionId_Returns404WithStructuredError()
    {
        var payload = new
        {
            sessionId = "missing-session-id",
            action = new { type = "swap", indices = new[] { 0, 1 } }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("status").GetString().Should().Be("error");
        root.GetProperty("message").GetString().Should().Contain("not found");
    }

    [Fact(DisplayName = "BE-IT-SIM-14 - POST /api/simulation/validate-step validates missing fields")]
    public async Task ValidateStep_WithMissingSessionIdTypeOrIndices_Returns400ValidationBody()
    {
        var payload = new
        {
            sessionId = "",
            action = new { type = "", indices = new[] { 0 } }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
    }

    private async Task<string> StartSessionAsync(int[] array)
    {
        using var doc = await StartSessionResponseAsync(array);
        return doc.RootElement.GetProperty("sessionId").GetString()!;
    }

    private async Task<JsonDocument> StartSessionResponseAsync(int[] array)
    {
        var payload = new { algorithm = "bubble_sort", array };
        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
