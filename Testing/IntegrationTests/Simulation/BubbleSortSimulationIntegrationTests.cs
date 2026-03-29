using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using backend.Models.Simulations;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Xunit;

namespace IntegrationTests.Simulation;

public class BubbleSortSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BubbleSortSimulationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> ParseBodyAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
        return JsonDocument.Parse(body);
    }

    private static async Task AssertValidationBadRequestAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var doc = await ParseBodyAsync(response);
        var root = doc.RootElement;

        var hasStandardEnvelope = root.TryGetProperty("statusCode", out var statusCodeProp)
            && root.TryGetProperty("message", out _);

        if (hasStandardEnvelope)
        {
            statusCodeProp.GetInt32().Should().Be(400);
        }
        else
        {
            root.TryGetProperty("status", out var statusProp).Should().BeTrue();
            statusProp.GetInt32().Should().Be(400);
        }

        root.TryGetProperty("errors", out var errorsEl).Should().BeTrue();
        errorsEl.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "BE-IT-SIM-01 — POST /api/simulation/run returns full bubble sort trace for valid unsorted input")]
    public async Task Run_ReturnsFullBubbleSortTrace_ForValidUnsortedInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "bubble_sort",
            array = new[] { 5, 1, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Bubble Sort");
        body.TotalSteps.Should().Be(body.Steps.Count);

        var labels = body.Steps.Select(s => s.ActionLabel).ToArray();
        labels.Should().Contain("compare");
        labels.Should().Contain("swap");
        labels.Should().Contain("complete");
    }

    [Theory(DisplayName = "BE-IT-SIM-02 — POST /api/simulation/run accepts normalized algorithm keys")]
    [InlineData("bubble_sort")]
    [InlineData("bubble-sort")]
    [InlineData("  BuBbLe_SoRt  ")]
    public async Task Run_AcceptsNormalizedAlgorithmKeys(string algorithm)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm,
            array = new[] { 4, 2, 3, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Bubble Sort");
    }

    [Fact(DisplayName = "BE-IT-SIM-03 — POST /api/simulation/run returns 501 for unsupported algorithm")]
    public async Task Run_Returns501_ForUnsupportedAlgorithm()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "linear_search",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not supported");
    }

    [Theory(DisplayName = "BE-IT-SIM-04 — POST /api/simulation/run validates empty algorithm or empty array")]
    [InlineData("", new[] { 1, 2, 3 })]
    [InlineData("bubble_sort", new int[0])]
    public async Task Run_ValidatesEmptyAlgorithmOrArray(string algorithm, int[] array)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm,
            array
        });

        await AssertValidationBadRequestAsync(response);
    }

    [Fact(DisplayName = "BE-IT-SIM-05 — POST /api/simulation/start creates practice session for valid bubble sort input")]
    public async Task Start_CreatesPracticeSession_ForValidBubbleSortInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 5, 1, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();
        session.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().BeLessThan(session.Steps.Count);
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap");
    }

    [Fact(DisplayName = "BE-IT-SIM-06 — POST /api/simulation/start handles already sorted input")]
    public async Task Start_HandlesAlreadySortedInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 1, 2, 3, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var action = session!.Steps[session.CurrentStepIndex].ActionLabel;
        action.Should().BeOneOf("early_exit", "complete");
    }

    [Fact(DisplayName = "BE-IT-SIM-07 — POST /api/simulation/start returns 501 for unsupported algorithm")]
    public async Task Start_Returns501_ForUnsupportedAlgorithm()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "linear_search",
            array = new[] { 3, 2, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not supported");
    }

    [Theory(DisplayName = "BE-IT-SIM-08 — POST /api/simulation/start validates empty algorithm or empty array")]
    [InlineData("", new[] { 1, 2 })]
    [InlineData("bubble_sort", new int[0])]
    public async Task Start_ValidatesEmptyAlgorithmOrArray(string algorithm, int[] array)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm,
            array
        });

        await AssertValidationBadRequestAsync(response);
    }

    [Fact(DisplayName = "BE-IT-SIM-09 — POST /api/simulation/validate-step accepts correct swap and advances session")]
    public async Task ValidateStep_AcceptsCorrectSwap_AndAdvancesSession()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 5, 1, 4 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        var expectedStep = session!.Steps[session.CurrentStepIndex];
        expectedStep.ActionLabel.Should().Be("swap");

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new
            {
                type = "swap",
                indices = expectedStep.ActiveIndices
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeTrue();
        body.NewArrayState.Should().Equal(expectedStep.ArrayState);
        body.NextExpectedAction.Should().Be("swap");
        body.CurrentStepIndex.Should().BeGreaterThan(session.CurrentStepIndex);
    }

    [Fact(DisplayName = "BE-IT-SIM-10 — POST /api/simulation/validate-step rejects incorrect swap without advancing")]
    public async Task ValidateStep_RejectsIncorrectSwap_WithoutAdvancingSession()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 5, 1, 4 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var expectedStep = session!.Steps[session.CurrentStepIndex];
        var currentArrayState = session.Steps[session.CurrentStepIndex - 1].ArrayState;

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new
            {
                type = "swap",
                indices = new[] { expectedStep.ActiveIndices[1], expectedStep.ActiveIndices[0] }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeFalse();
        body.NewArrayState.Should().Equal(currentArrayState);
        body.Hint.Should().Contain("Try swapping index");
        body.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
    }

    [Fact(DisplayName = "BE-IT-SIM-11 — POST /api/simulation/validate-step returns practice-complete response at terminal state")]
    public async Task ValidateStep_ReturnsPracticeCompleteResponse_AtTerminalState()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 1, 2, 3, 4 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session!.SessionId,
            action = new
            {
                type = "swap",
                indices = new[] { 0, 1 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeFalse();
        body.NextExpectedAction.Should().Be("complete");
        body.Message.Should().Be("Practice complete.");
        body.Hint.Should().Be("No more actions are needed.");
    }

    [Fact(DisplayName = "BE-IT-SIM-12 — POST /api/simulation/validate-step accepts legacy userAction alias")]
    public async Task ValidateStep_AcceptsLegacyUserActionAlias()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 5, 1, 4 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var expectedStep = session!.Steps[session.CurrentStepIndex];

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            userAction = new
            {
                type = "swap",
                indices = expectedStep.ActiveIndices
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SIM-13 — POST /api/simulation/validate-step returns 404 for unknown session id")]
    public async Task ValidateStep_Returns404_ForUnknownSessionId()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = "does-not-exist",
            action = new
            {
                type = "swap",
                indices = new[] { 0, 1 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("was not found");
    }

    [Theory(DisplayName = "BE-IT-SIM-14 — POST /api/simulation/validate-step validates missing session id, action type, or indices")]
    [InlineData("{ \"action\": { \"type\": \"swap\", \"indices\": [0, 1] } }", "SessionId")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"indices\": [0, 1] } }", "Action.Type")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"type\": \"swap\", \"indices\": [0] } }", "Action.Indices")]
    public async Task ValidateStep_ValidatesMissingFields(string payload, string expectedErrorKey)
    {
        var response = await _client.PostAsync("/api/simulation/validate-step", Json(payload));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty(expectedErrorKey, out _).Should().BeTrue();
    }
}
