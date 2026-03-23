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

public class BinarySearchSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BinarySearchSimulationIntegrationTests(CustomWebApplicationFactory factory)
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

    private async Task<SimulationValidationResponse> ValidateStepAsync(string sessionId, string type, params int[] indices)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId,
            action = new
            {
                type,
                indices
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    [Fact(DisplayName = "BE-IT-BS-01 — POST /api/simulation/run returns a binary search trace when the target exists")]
    public async Task Run_ReturnsBinarySearchTrace_WhenTargetExists()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 },
            target = 7
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Binary Search");
        body.TotalSteps.Should().Be(body.Steps.Count);

        var labels = body.Steps.Select(step => step.ActionLabel).ToArray();
        labels.Should().Contain("midpoint_pick");
        labels.Should().Contain("discard_left");
        labels.Should().Contain("found");
        labels[^1].Should().Be("found");
    }

    [Fact(DisplayName = "BE-IT-BS-02 — POST /api/simulation/run returns a complete trace when the target does not exist")]
    public async Task Run_ReturnsCompleteTrace_WhenTargetDoesNotExist()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 },
            target = 8
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Binary Search");
        body.TotalSteps.Should().Be(body.Steps.Count);

        var labels = body.Steps.Select(step => step.ActionLabel).ToArray();
        labels.Should().Contain("midpoint_pick");
        labels.Should().Contain("discard_left");
        labels.Should().Contain("discard_right");
        labels.Should().Contain("not_found");
        labels.Should().NotContain("found");
        labels[^1].Should().Be("not_found");
    }

    [Fact(DisplayName = "BE-IT-BS-03 — POST /api/simulation/start creates a binary search practice session")]
    public async Task Start_CreatesBinarySearchPracticeSession()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "binary_search",
            array = new[] { 2, 4, 6, 8, 10 },
            target = 8
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();
        session.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().BeLessThan(session.Steps.Count);

        var currentStep = session.Steps[session.CurrentStepIndex];
        currentStep.ActionLabel.Should().Be("midpoint_pick");
        currentStep.ActiveIndices.Should().Equal(2);
        currentStep.Search.Should().NotBeNull();
        currentStep.Search!.LowIndex.Should().Be(0);
        currentStep.Search.HighIndex.Should().Be(4);
        currentStep.Search.MidpointIndex.Should().Be(2);
    }

    [Fact(DisplayName = "BE-IT-BS-04 — POST /api/simulation/validate-step accepts a correct binary search action and advances the range")]
    public async Task ValidateStep_AcceptsCorrectAction_AndAdvancesSession()
    {
        var startResponse = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "binary_search",
            array = new[] { 2, 4, 6, 8, 10 },
            target = 8
        });

        var session = await startResponse.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var initialStep = session!.Steps[session.CurrentStepIndex];
        initialStep.ActionLabel.Should().Be("midpoint_pick");

        var body = await ValidateStepAsync(session.SessionId, "midpoint_pick", initialStep.ActiveIndices[0]);

        body.Correct.Should().BeTrue();
        body.CurrentStepIndex.Should().BeGreaterThan(session.CurrentStepIndex);
        body.NextExpectedAction.Should().BeOneOf("midpoint_pick", "complete");
    }

    [Fact(DisplayName = "BE-IT-BS-05 — POST /api/simulation/validate-step rejects an incorrect binary search action without advancing")]
    public async Task ValidateStep_RejectsIncorrectAction_WithoutAdvancingSession()
    {
        var startResponse = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "binary_search",
            array = new[] { 2, 4, 6, 8, 10 },
            target = 8
        });

        var session = await startResponse.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var response = await ValidateStepAsync(session!.SessionId, "midpoint_pick", 0);

        response.Correct.Should().BeFalse();
        response.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
        response.Hint.Should().Contain("Pick the midpoint at index");
        response.NextExpectedAction.Should().Be("midpoint_pick");
    }

    [Fact(DisplayName = "BE-IT-BS-06 — Binary search practice completes correctly for both found and not-found endings")]
    public async Task Practice_CompletesCorrectly_ForFoundAndNotFoundEndings()
    {
        var foundStart = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 },
            target = 7
        });

        var foundSession = await foundStart.Content.ReadFromJsonAsync<SimulationSession>();
        foundSession.Should().NotBeNull();

        var foundStep1 = await ValidateStepAsync(foundSession!.SessionId, "midpoint_pick", 2);
        foundStep1.Correct.Should().BeTrue();
        foundStep1.NextExpectedAction.Should().Be("midpoint_pick");

        var foundStep2 = await ValidateStepAsync(foundSession.SessionId, "midpoint_pick", 3);
        foundStep2.Correct.Should().BeTrue();
        foundStep2.NextExpectedAction.Should().Be("target_found");

        var foundTerminal = await ValidateStepAsync(foundSession.SessionId, "midpoint_pick", 3);
        foundTerminal.Correct.Should().BeFalse();
        foundTerminal.NextExpectedAction.Should().Be("target_found");
        foundTerminal.Message.Should().Be("Practice complete.");
        foundTerminal.Hint.Should().Be("No more actions are needed.");

        var notFoundStart = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 },
            target = 8
        });

        var notFoundSession = await notFoundStart.Content.ReadFromJsonAsync<SimulationSession>();
        notFoundSession.Should().NotBeNull();

        var notFoundStep1 = await ValidateStepAsync(notFoundSession!.SessionId, "midpoint_pick", 2);
        notFoundStep1.Correct.Should().BeTrue();

        var notFoundStep2 = await ValidateStepAsync(notFoundSession.SessionId, "midpoint_pick", 3);
        notFoundStep2.Correct.Should().BeTrue();

        var notFoundStep3 = await ValidateStepAsync(notFoundSession.SessionId, "midpoint_pick", 4);
        notFoundStep3.Correct.Should().BeTrue();
        notFoundStep3.NextExpectedAction.Should().Be("target_not_found");

        var notFoundDecision3 = await ValidateStepAsync(notFoundSession.SessionId, "discard_right");
        notFoundDecision3.Correct.Should().BeFalse();
        notFoundDecision3.NextExpectedAction.Should().Be("target_not_found");
        notFoundDecision3.Message.Should().Be("Practice complete.");
        notFoundDecision3.Hint.Should().Be("No more actions are needed.");
    }

    [Fact(DisplayName = "BE-IT-BS-07 — Binary search request validation rejects missing required search inputs")]
    public async Task Run_RejectsMissingTarget_ForBinarySearch()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 }
        });

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        var hasTargetError = errors.TryGetProperty("Target", out _)
            || errors.TryGetProperty("target", out _);
        hasTargetError.Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-BS-08 — Binary search enforces the sorted-array contract")]
    public async Task Run_NormalizesUnsortedInput_AndReturnsSortedTrace()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "binary_search",
            array = new[] { 9, 1, 7, 3, 5 },
            target = 7
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.Steps.Should().NotBeEmpty();

        var expectedSorted = new[] { 1, 3, 5, 7, 9 };
        body.Steps[0].ArrayState.Should().Equal(expectedSorted);
        body.Steps.Should().OnlyContain(step => step.ArrayState.SequenceEqual(expectedSorted));
    }
}
