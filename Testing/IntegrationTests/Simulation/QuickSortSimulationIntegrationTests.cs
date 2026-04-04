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

public class QuickSortSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public QuickSortSimulationIntegrationTests(CustomWebApplicationFactory factory)
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

    private static string NormalizeAction(string actionLabel)
    {
        return actionLabel.Trim().ToLowerInvariant() switch
        {
            "pivot_swap" => "swap",
            _ => actionLabel.Trim().ToLowerInvariant()
        };
    }

    private static bool IsQuickSortInteractive(SimulationStep step)
    {
        var normalized = step.ActionLabel.Trim().ToLowerInvariant();
        return normalized is "compare" or "swap" or "pivot_swap";
    }

    private static int FindFirstQuickSortInteractiveIndex(SimulationSession session)
    {
        return session.Steps.FindIndex(IsQuickSortInteractive);
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

    private async Task<SimulationSession> StartSessionAsync(string algorithm, int[] array)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm,
            array
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        return session!;
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

    private async Task<int> AdvanceSessionUntilActionAsync(SimulationSession session, string actionLabel)
    {
        var currentIndex = session.CurrentStepIndex;

        while (session.Steps[currentIndex].ActionLabel != actionLabel)
        {
            var expectedStep = session.Steps[currentIndex];
            var response = await ValidateStepAsync(
                session.SessionId,
                NormalizeAction(expectedStep.ActionLabel),
                expectedStep.ActiveIndices);

            response.Correct.Should().BeTrue();
            currentIndex = response.CurrentStepIndex;
        }

        return currentIndex;
    }

    [Fact(DisplayName = "BE-IT-QS-01 - POST /api/simulation/start returns the first Quick Sort compare step with metadata intact")]
    public async Task Start_ReturnsFirstInteractiveCompareStep_WithQuickSortAndRecursionMetadata()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "quick_sort",
            array = new[] { 5, 1, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = await ParseBodyAsync(response);
        var root = document.RootElement;
        var stepsElement = root.GetProperty("steps");
        var session = JsonSerializer.Deserialize<SimulationSession>(
            root.GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();

        var firstInteractiveIndex = FindFirstQuickSortInteractiveIndex(session);
        firstInteractiveIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().Be(firstInteractiveIndex);

        var currentStep = session.Steps[session.CurrentStepIndex];
        currentStep.ActionLabel.Should().Be("compare");
        currentStep.QuickSort.Should().NotBeNull();
        currentStep.QuickSort!.Type.Should().Be("compare");
        currentStep.QuickSort.Range.Should().HaveCount(2);
        currentStep.Recursion.Should().NotBeNull();
        currentStep.Recursion!.State.Should().Be("compare");
        currentStep.Recursion.Stack.Should().NotBeEmpty();
        currentStep.ActiveIndices.Should().HaveCount(2);

        var currentStepJson = stepsElement[session.CurrentStepIndex];
        currentStepJson.GetProperty("actionLabel").GetString().Should().Be("compare");
        currentStepJson.TryGetProperty("quickSort", out var quickSortJson).Should().BeTrue();
        quickSortJson.ValueKind.Should().Be(JsonValueKind.Object);
        currentStepJson.TryGetProperty("recursion", out var recursionJson).Should().BeTrue();
        recursionJson.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "BE-IT-QS-02 - POST /api/simulation/validate-step accepts compare actions and returns compare hints")]
    public async Task ValidateStep_AcceptsCompareAction_AndReturnsCompareHints()
    {
        var session = await StartSessionAsync("quick_sort", [5, 1, 4]);
        var compareStep = session.Steps[session.CurrentStepIndex];

        compareStep.ActionLabel.Should().Be("compare");

        var body = await ValidateStepAsync(session.SessionId, "compare", compareStep.ActiveIndices);

        body.Correct.Should().BeTrue();
        body.CurrentStepIndex.Should().BeGreaterThan(session.CurrentStepIndex);
        body.NextExpectedAction.Should().Be("compare");
        body.Hint.Should().Contain("Compare index");
        body.SuggestedIndices.Should().Equal(session.Steps[body.CurrentStepIndex].ActiveIndices);
        body.NewArrayState.Should().Equal(compareStep.ArrayState);
    }

    [Fact(DisplayName = "BE-IT-QS-03 - POST /api/simulation/validate-step accepts pivot_swap when sent as swap")]
    public async Task ValidateStep_AcceptsPivotSwap_AsSwapAction()
    {
        var session = await StartSessionAsync("quick_sort", [5, 1, 4]);
        var currentIndex = session.CurrentStepIndex;
        SimulationValidationResponse? lastResponse = null;

        while (session.Steps[currentIndex].ActionLabel != "pivot_swap")
        {
            var step = session.Steps[currentIndex];
            lastResponse = await ValidateStepAsync(session.SessionId, NormalizeAction(step.ActionLabel), step.ActiveIndices);
            lastResponse.Correct.Should().BeTrue();
            currentIndex = lastResponse.CurrentStepIndex;
        }

        var pivotSwapStep = session.Steps[currentIndex];
        pivotSwapStep.ActionLabel.Should().Be("pivot_swap");
        pivotSwapStep.QuickSort.Should().NotBeNull();
        pivotSwapStep.QuickSort!.Type.Should().Be("pivot_swap");

        var response = await ValidateStepAsync(session.SessionId, "swap", pivotSwapStep.ActiveIndices);

        response.Correct.Should().BeTrue();
        response.NewArrayState.Should().Equal(pivotSwapStep.ArrayState);
        response.CurrentStepIndex.Should().BeGreaterThan(currentIndex);
        response.NextExpectedAction.Should().Be("complete");
        response.Hint.Should().Be("No more actions are needed.");
    }

    [Fact(DisplayName = "BE-IT-QS-04 - Quick Sort session preserves metadata and recursion-driven progression across multiple requests")]
    public async Task Session_PreservesQuickSortAndRecursionDrivenFlow_AcrossMultipleRequests()
    {
        var session = await StartSessionAsync("quick_sort", [8, 3, 5, 1, 7]);
        var currentIndex = session.CurrentStepIndex;

        session.Steps.Should().OnlyContain(step =>
            step.QuickSort != null && step.Recursion != null,
            "Quick Sort sessions should preserve both metadata models on every step");

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var expectedStep = session.Steps[currentIndex];
            IsQuickSortInteractive(expectedStep).Should().BeTrue();
            expectedStep.QuickSort.Should().NotBeNull();
            expectedStep.Recursion.Should().NotBeNull();
            expectedStep.Recursion!.Stack.Should().NotBeNull();

            var response = await ValidateStepAsync(
                session.SessionId,
                NormalizeAction(expectedStep.ActionLabel),
                expectedStep.ActiveIndices);

            response.Correct.Should().BeTrue();
            response.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(currentIndex);

            currentIndex = response.CurrentStepIndex;
            var nextStep = session.Steps[currentIndex];
            nextStep.QuickSort.Should().NotBeNull();
            nextStep.Recursion.Should().NotBeNull();
            nextStep.Recursion!.Stack.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "BE-IT-QS-05 - Single-element Quick Sort sessions complete without requiring interactive steps")]
    public async Task Start_SingleElementQuickSort_CompletesWithoutInteractiveSteps()
    {
        var session = await StartSessionAsync("quick_sort", [42]);

        session.Steps.Should().NotContain(step => IsQuickSortInteractive(step));
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("complete");

        var response = await ValidateStepAsync(session.SessionId, "compare", 0, 0);

        response.Correct.Should().BeFalse();
        response.NextExpectedAction.Should().Be("complete");
        response.Message.Should().Be("Practice complete.");
        response.Hint.Should().Be("No more actions are needed.");
    }

    [Fact(DisplayName = "BE-IT-QS-06 - Regression safety: Bubble Sort still seeds practice at swap")]
    public async Task Regression_BubbleSort_StartsAtSwap_NotCompare()
    {
        var session = await StartSessionAsync("bubble_sort", [5, 1, 4]);

        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap");
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().NotBe("compare");
    }

    [Fact(DisplayName = "BE-IT-QS-07 - Regression safety: Binary Search session behavior remains unchanged")]
    public async Task Regression_BinarySearch_StartsAtMidpointPick_WithSearchMetadata()
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
        session!.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("midpoint_pick");
        session.Steps[session.CurrentStepIndex].Search.Should().NotBeNull();
        session.Steps[session.CurrentStepIndex].Search!.MidpointIndex.Should().Be(2);
    }

    [Theory(DisplayName = "BE-IT-QS-08 - Quick Sort start and validate-step still enforce required fields")]
    [InlineData("{ \"array\": [5,1,4] }", "Algorithm")]
    [InlineData("{ \"algorithm\": \"quick_sort\", \"array\": [] }", "Array")]
    public async Task Start_ValidatesRequiredFields(string payload, string expectedErrorKey)
    {
        var response = await _client.PostAsync("/api/simulation/start", Json(payload));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty(expectedErrorKey, out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-QS-09 - POST /api/simulation/run returns a full Quick Sort trace with quick-sort metadata")]
    public async Task Run_ReturnsFullQuickSortTrace_WithQuickSortMetadata()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "quick_sort",
            array = new[] { 8, 3, 5, 1, 7 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Quick Sort");
        body.TotalSteps.Should().Be(body.Steps.Count);
        body.Steps.Should().OnlyContain(step => step.QuickSort != null && step.Recursion != null);

        var labels = body.Steps.Select(step => step.ActionLabel).ToArray();
        labels.Should().Contain("pivot_select");
        labels.Should().Contain("compare");
        labels.Should().Contain("pivot_placed");
        labels.Should().Contain("complete");
        body.Steps.Last().ArrayState.Should().Equal([1, 3, 5, 7, 8]);
    }

    [Fact(DisplayName = "BE-IT-QS-10 - POST /api/simulation/run accepts the 'quick-sort' HTTP algorithm alias")]
    public async Task Run_AcceptsQuickSortHyphenAlias()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "quick-sort",
            array = new[] { 4, 1, 3, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Quick Sort");
        body.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4]);
    }

    [Fact(DisplayName = "BE-IT-QS-11 - Quick Sort practice completes through a full walkthrough for a larger input")]
    public async Task Session_FullWalkthrough_CompletesAndProducesSortedArray()
    {
        var session = await StartSessionAsync("quick_sort", [8, 3, 5, 1, 7]);
        var currentIndex = session.CurrentStepIndex;
        SimulationValidationResponse? lastResponse = null;

        while (session.Steps[currentIndex].ActionLabel != "complete")
        {
            var expectedStep = session.Steps[currentIndex];
            IsQuickSortInteractive(expectedStep).Should().BeTrue();

            lastResponse = await ValidateStepAsync(
                session.SessionId,
                NormalizeAction(expectedStep.ActionLabel),
                expectedStep.ActiveIndices);

            lastResponse.Correct.Should().BeTrue();
            currentIndex = lastResponse.CurrentStepIndex;
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.NextExpectedAction.Should().Be("complete");
        lastResponse.Hint.Should().Be("No more actions are needed.");
        lastResponse.NewArrayState.Should().Equal([1, 3, 5, 7, 8]);
        session.Steps[currentIndex].ActionLabel.Should().Be("complete");
        session.Steps[currentIndex].ArrayState.Should().Equal([1, 3, 5, 7, 8]);
    }

    [Fact(DisplayName = "BE-IT-QS-12 - Duplicate-heavy Quick Sort practice still validates correctly through to completion")]
    public async Task Session_DuplicateHeavyInput_CompletesWithSortedFinalArray()
    {
        var session = await StartSessionAsync("quick_sort", [5, 3, 5, 1, 5, 2]);
        var currentIndex = session.CurrentStepIndex;
        SimulationValidationResponse? lastResponse = null;

        while (session.Steps[currentIndex].ActionLabel != "complete")
        {
            var expectedStep = session.Steps[currentIndex];
            IsQuickSortInteractive(expectedStep).Should().BeTrue();

            lastResponse = await ValidateStepAsync(
                session.SessionId,
                NormalizeAction(expectedStep.ActionLabel),
                expectedStep.ActiveIndices);

            lastResponse.Correct.Should().BeTrue();
            currentIndex = lastResponse.CurrentStepIndex;
        }

        lastResponse.Should().NotBeNull();
        lastResponse!.NextExpectedAction.Should().Be("complete");
        session.Steps[currentIndex].ArrayState.Should().Equal([1, 2, 3, 5, 5, 5]);
    }

    [Fact(DisplayName = "BE-IT-QS-13 - POST /api/simulation/validate-step rejects the wrong quick-sort action without advancing")]
    public async Task ValidateStep_RejectsWrongAction_WithoutAdvancingSession()
    {
        var session = await StartSessionAsync("quick_sort", [5, 1, 4]);
        var expectedStep = session.Steps[session.CurrentStepIndex];

        expectedStep.ActionLabel.Should().Be("compare");

        var response = await ValidateStepAsync(session.SessionId, "swap", expectedStep.ActiveIndices);

        response.Correct.Should().BeFalse();
        response.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
        response.NextExpectedAction.Should().Be("compare");
        response.Hint.Should().Contain("Compare index");
        response.SuggestedIndices.Should().Equal(expectedStep.ActiveIndices);
        response.NewArrayState.Should().Equal(expectedStep.ArrayState);
    }

    [Fact(DisplayName = "BE-IT-QS-14 - POST /api/simulation/validate-step rejects wrong compare indices without advancing")]
    public async Task ValidateStep_RejectsWrongCompareIndices_WithoutAdvancingSession()
    {
        var session = await StartSessionAsync("quick_sort", [5, 1, 4]);
        var expectedStep = session.Steps[session.CurrentStepIndex];

        expectedStep.ActionLabel.Should().Be("compare");

        var response = await ValidateStepAsync(
            session.SessionId,
            "compare",
            expectedStep.ActiveIndices.Reverse().ToArray());

        response.Correct.Should().BeFalse();
        response.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
        response.NextExpectedAction.Should().Be("compare");
        response.Hint.Should().Contain("Compare index");
        response.SuggestedIndices.Should().Equal(expectedStep.ActiveIndices);
    }

    [Fact(DisplayName = "BE-IT-QS-15 - POST /api/simulation/validate-step rejects reversed pivot-swap indices")]
    public async Task ValidateStep_RejectsReversedPivotSwapIndices()
    {
        var session = await StartSessionAsync("quick_sort", [5, 1, 4]);
        var pivotSwapIndex = await AdvanceSessionUntilActionAsync(session, "pivot_swap");
        var pivotSwapStep = session.Steps[pivotSwapIndex];

        var response = await ValidateStepAsync(
            session.SessionId,
            "swap",
            pivotSwapStep.ActiveIndices.Reverse().ToArray());

        response.Correct.Should().BeFalse();
        response.CurrentStepIndex.Should().Be(pivotSwapIndex);
        response.NextExpectedAction.Should().Be("swap");
        response.Hint.Should().Contain("Try swapping index");
        response.SuggestedIndices.Should().Equal(pivotSwapStep.ActiveIndices);
    }

    [Fact(DisplayName = "BE-IT-QS-16 - POST /api/simulation/validate-step rejects explicit 'pivot_swap' user actions and requires the swap alias")]
    public async Task ValidateStep_RejectsExplicitPivotSwapAction()
    {
        var session = await StartSessionAsync("quick_sort", [5, 1, 4]);
        var pivotSwapIndex = await AdvanceSessionUntilActionAsync(session, "pivot_swap");
        var pivotSwapStep = session.Steps[pivotSwapIndex];

        var response = await ValidateStepAsync(session.SessionId, "pivot_swap", pivotSwapStep.ActiveIndices);

        response.Correct.Should().BeFalse();
        response.CurrentStepIndex.Should().Be(pivotSwapIndex);
        response.NextExpectedAction.Should().Be("swap");
        response.SuggestedIndices.Should().Equal(pivotSwapStep.ActiveIndices);
    }

    [Fact(DisplayName = "BE-IT-QS-17 - POST /api/simulation/validate-step returns 404 for an unknown quick-sort session id")]
    public async Task ValidateStep_UnknownSessionId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = "missing-quick-sort-session",
            action = new
            {
                type = "compare",
                indices = new[] { 0, 1 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("missing-quick-sort-session");
    }

    [Theory(DisplayName = "BE-IT-QS-18 - POST /api/simulation/validate-step validates missing or malformed quick-sort action payloads")]
    [InlineData("{ \"action\": { \"type\": \"compare\", \"indices\": [0, 1] } }", "SessionId")]
    [InlineData("{ \"sessionId\": \"abc\" }", "Action")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"indices\": [0, 1] } }", "Action.Type")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"type\": \"compare\", \"indices\": [0] } }", "Action.Indices")]
    public async Task ValidateStep_ValidatesMissingOrMalformedFields(string payload, string expectedErrorKey)
    {
        var response = await _client.PostAsync("/api/simulation/validate-step", Json(payload));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty(expectedErrorKey, out _).Should().BeTrue();
    }
}
