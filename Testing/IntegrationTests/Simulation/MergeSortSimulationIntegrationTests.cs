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

public class MergeSortSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MergeSortSimulationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);
    }

    private static StringContent Json(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static bool IsMergeSortInteractive(SimulationStep step)
    {
        var normalized = step.ActionLabel.Trim().ToLowerInvariant();
        return normalized is "compare" or "place";
    }

    private static int FindFirstMergeSortInteractiveIndex(SimulationSession session)
    {
        return session.Steps.FindIndex(IsMergeSortInteractive);
    }

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

    [Fact(DisplayName = "BE-IT-MS-01 - POST /api/simulation/run returns a full Merge Sort trace with merge metadata")]
    public async Task Run_ReturnsFullMergeSortTrace_WithMergeMetadata()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "merge_sort",
            array = new[] { 5, 1, 4, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Merge Sort");
        body.TotalSteps.Should().Be(body.Steps.Count);
        body.Steps.Should().OnlyContain(step => step.MergeSort != null && step.Recursion != null);

        var labels = body.Steps.Select(step => step.ActionLabel).ToArray();
        labels.Should().Contain("split");
        labels.Should().Contain("compare");
        labels.Should().Contain("place");
        labels.Should().Contain("complete");
        body.Steps.Last().ArrayState.Should().Equal([1, 2, 4, 5]);
    }

    [Fact(DisplayName = "BE-IT-MS-02 - POST /api/simulation/start returns the first Merge Sort compare step with metadata intact")]
    public async Task Start_ReturnsFirstInteractiveCompareStep_WithMergeSortAndRecursionMetadata()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "merge_sort",
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

        var firstInteractiveIndex = FindFirstMergeSortInteractiveIndex(session);
        firstInteractiveIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().Be(firstInteractiveIndex);

        var currentStep = session.Steps[session.CurrentStepIndex];
        currentStep.ActionLabel.Should().Be("compare");
        currentStep.MergeSort.Should().NotBeNull();
        currentStep.MergeSort!.Type.Should().Be("compare");
        currentStep.MergeSort.Left.Should().Be(0);
        currentStep.MergeSort.Right.Should().Be(1);
        currentStep.MergeSort.Mid.Should().Be(0);
        currentStep.MergeSort.MergeBuffer.Should().NotBeNull();
        currentStep.MergeSort.PlaceIndex.Should().BeNull();
        currentStep.Recursion.Should().NotBeNull();
        currentStep.Recursion!.State.Should().Be("compare");
        currentStep.Recursion.Stack.Should().NotBeEmpty();
        currentStep.ActiveIndices.Should().HaveCount(2);

        var currentStepJson = stepsElement[session.CurrentStepIndex];
        currentStepJson.GetProperty("actionLabel").GetString().Should().Be("compare");
        currentStepJson.TryGetProperty("mergeSort", out var mergeSortJson).Should().BeTrue();
        mergeSortJson.ValueKind.Should().Be(JsonValueKind.Object);
        currentStepJson.TryGetProperty("recursion", out var recursionJson).Should().BeTrue();
        recursionJson.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact(DisplayName = "BE-IT-MS-03 - POST /api/simulation/validate-step accepts compare actions and returns place hints")]
    public async Task ValidateStep_AcceptsCompareAction_AndReturnsPlaceHint()
    {
        var session = await StartSessionAsync("merge_sort", [5, 1, 4]);
        var compareStep = session.Steps[session.CurrentStepIndex];

        compareStep.ActionLabel.Should().Be("compare");

        var body = await ValidateStepAsync(session.SessionId, "compare", compareStep.ActiveIndices);

        body.Correct.Should().BeTrue();
        body.CurrentStepIndex.Should().BeGreaterThan(session.CurrentStepIndex);
        body.NextExpectedAction.Should().Be("place");
        body.Hint.Should().Contain("Place the selected value at index");
        body.SuggestedIndices.Should().Equal(session.Steps[body.CurrentStepIndex].ActiveIndices);
        body.NewArrayState.Should().Equal(compareStep.ArrayState);
    }

    [Fact(DisplayName = "BE-IT-MS-04 - POST /api/simulation/validate-step accepts place actions and advances the array state")]
    public async Task ValidateStep_AcceptsPlaceAction_AndAdvancesArrayState()
    {
        var session = await StartSessionAsync("merge_sort", [5, 1, 4]);
        var compareStep = session.Steps[session.CurrentStepIndex];
        var compareResponse = await ValidateStepAsync(session.SessionId, "compare", compareStep.ActiveIndices);

        var placeStep = session.Steps[compareResponse.CurrentStepIndex];
        placeStep.ActionLabel.Should().Be("place");
        placeStep.MergeSort.Should().NotBeNull();
        placeStep.MergeSort!.PlaceIndex.Should().HaveValue();

        var placeResponse = await ValidateStepAsync(session.SessionId, "place", placeStep.ActiveIndices);

        placeResponse.Correct.Should().BeTrue();
        placeResponse.NewArrayState.Should().Equal(placeStep.ArrayState);
        placeResponse.CurrentStepIndex.Should().BeGreaterThan(compareResponse.CurrentStepIndex);
        placeResponse.NextExpectedAction.Should().Be("place");
    }

    [Fact(DisplayName = "BE-IT-MS-05 - Merge Sort session preserves metadata and recursion-driven progression across multiple requests")]
    public async Task Session_PreservesMergeSortAndRecursionMetadata_AcrossMultipleRequests()
    {
        var session = await StartSessionAsync("merge_sort", [8, 3, 5, 1, 7]);
        var currentIndex = session.CurrentStepIndex;

        session.Steps.Should().OnlyContain(step =>
            step.MergeSort != null && step.Recursion != null,
            "Merge Sort sessions should preserve both metadata models on every step");

        for (var iteration = 0; iteration < 4; iteration++)
        {
            var expectedStep = session.Steps[currentIndex];
            IsMergeSortInteractive(expectedStep).Should().BeTrue();
            expectedStep.MergeSort.Should().NotBeNull();
            expectedStep.Recursion.Should().NotBeNull();
            expectedStep.Recursion!.Stack.Should().NotBeNull();

            var response = await ValidateStepAsync(
                session.SessionId,
                expectedStep.ActionLabel,
                expectedStep.ActiveIndices);

            response.Correct.Should().BeTrue();
            response.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(currentIndex);

            currentIndex = response.CurrentStepIndex;
            var nextStep = session.Steps[currentIndex];
            nextStep.MergeSort.Should().NotBeNull();
            nextStep.Recursion.Should().NotBeNull();
            nextStep.Recursion!.Stack.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "BE-IT-MS-06 - Merge Sort practice completes through a full compare/place walkthrough")]
    public async Task Session_FullWalkthrough_CompletesAndProducesSortedArray()
    {
        var session = await StartSessionAsync("merge_sort", [8, 3, 5, 1, 7]);
        var currentIndex = session.CurrentStepIndex;
        SimulationValidationResponse? lastResponse = null;

        while (session.Steps[currentIndex].ActionLabel != "complete")
        {
            var expectedStep = session.Steps[currentIndex];
            IsMergeSortInteractive(expectedStep).Should().BeTrue();

            lastResponse = await ValidateStepAsync(
                session.SessionId,
                expectedStep.ActionLabel,
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

    [Fact(DisplayName = "BE-IT-MS-07 - POST /api/simulation/validate-step rejects the wrong merge action without advancing")]
    public async Task ValidateStep_RejectsWrongAction_WithoutAdvancingSession()
    {
        var session = await StartSessionAsync("merge_sort", [5, 1, 4]);
        var expectedStep = session.Steps[session.CurrentStepIndex];

        expectedStep.ActionLabel.Should().Be("compare");

        var response = await ValidateStepAsync(session.SessionId, "place", expectedStep.ActiveIndices);

        response.Correct.Should().BeFalse();
        response.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
        response.NextExpectedAction.Should().Be("compare");
        response.Hint.Should().Contain("Compare index");
        response.NewArrayState.Should().Equal(expectedStep.ArrayState);
    }

    [Fact(DisplayName = "BE-IT-MS-08 - POST /api/simulation/validate-step rejects wrong merge indices without advancing")]
    public async Task ValidateStep_RejectsWrongIndices_WithoutAdvancingSession()
    {
        var session = await StartSessionAsync("merge_sort", [5, 1, 4]);
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

    [Fact(DisplayName = "BE-IT-MS-09 - Single-element Merge Sort sessions complete without requiring interactive steps")]
    public async Task Start_SingleElementMergeSort_CompletesWithoutInteractiveSteps()
    {
        var session = await StartSessionAsync("merge_sort", [42]);

        session.Steps.Should().NotContain(step => IsMergeSortInteractive(step));
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("complete");

        var response = await ValidateStepAsync(session.SessionId, "compare", 0, 0);

        response.Correct.Should().BeFalse();
        response.NextExpectedAction.Should().Be("complete");
        response.Message.Should().Be("Practice complete.");
        response.Hint.Should().Be("No more actions are needed.");
    }

    [Theory(DisplayName = "BE-IT-MS-10 - Merge Sort start still enforces required fields")]
    [InlineData("{ \"array\": [5,1,4] }", "Algorithm")]
    [InlineData("{ \"algorithm\": \"merge_sort\", \"array\": [] }", "Array")]
    public async Task Start_ValidatesRequiredFields(string payload, string expectedErrorKey)
    {
        var response = await _client.PostAsync("/api/simulation/start", Json(payload));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty(expectedErrorKey, out _).Should().BeTrue();
    }

    [Theory(DisplayName = "BE-IT-MS-11 - Merge Sort validate-step still enforces required fields")]
    [InlineData("{ \"action\": { \"type\": \"compare\", \"indices\": [0, 1] } }", "SessionId")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"indices\": [0, 1] } }", "Action.Type")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"type\": \"compare\", \"indices\": [0] } }", "Action.Indices")]
    public async Task ValidateStep_ValidatesMissingFields(string payload, string expectedErrorKey)
    {
        var response = await _client.PostAsync("/api/simulation/validate-step", Json(payload));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty(expectedErrorKey, out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-MS-12 - Regression safety: Quick Sort still seeds practice at compare")]
    public async Task Regression_QuickSort_StartsAtCompare_WithQuickSortMetadata()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "quick_sort",
            array = new[] { 5, 1, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("compare");
        session.Steps[session.CurrentStepIndex].QuickSort.Should().NotBeNull();
        session.Steps[session.CurrentStepIndex].MergeSort.Should().BeNull();
    }

    [Fact(DisplayName = "BE-IT-MS-13 - Regression safety: Bubble Sort still seeds practice at swap")]
    public async Task Regression_BubbleSort_StartsAtSwap_NotCompareOrPlace()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "bubble_sort",
            array = new[] { 5, 1, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap");
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().NotBe("compare");
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().NotBe("place");
    }
}
