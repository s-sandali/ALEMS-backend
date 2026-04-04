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

public class HeapSortSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public HeapSortSimulationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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

    private async Task<SimulationSession> StartSessionAsync(string algorithm, int[] array)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new { algorithm, array });
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
            action = new { type, indices }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        return body!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/simulation/run — FULL TRACE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-IT-HEAP-01 — POST /api/simulation/run returns full heap sort trace for valid unsorted input")]
    public async Task Run_ReturnsFullHeapSortTrace_ForValidUnsortedInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = new[] { 3, 1, 4, 1, 5 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Heap Sort");
        body.TotalSteps.Should().Be(body.Steps.Count);

        var labels = body.Steps.Select(s => s.ActionLabel).ToArray();
        labels.Should().Contain("swap");
        labels.Should().Contain("complete");
    }

    [Theory(DisplayName = "BE-IT-HEAP-02 — POST /api/simulation/run accepts normalized heap sort algorithm keys")]
    [InlineData("heap_sort")]
    [InlineData("heap-sort")]
    [InlineData("HEAP_SORT")]
    [InlineData("  HeAp_SoRt  ")]
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
        body!.AlgorithmName.Should().Be("Heap Sort");
    }

    [Fact(DisplayName = "BE-IT-HEAP-03 — POST /api/simulation/run final array state is sorted ascending")]
    public async Task Run_FinalArrayState_IsSortedAscending()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = new[] { 5, 3, 8, 1, 9, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();

        var finalState = body!.Steps.Last().ArrayState;
        finalState.Should().BeInAscendingOrder(
            because: "heap sort must produce a fully sorted array");
    }

    [Fact(DisplayName = "BE-IT-HEAP-04 — POST /api/simulation/run response steps carry Heap metadata with valid Phase")]
    public async Task Run_Steps_ContainHeapMetadata()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = new[] { 4, 2, 7, 1, 8 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = await ParseBodyAsync(response);
        var stepsElement = doc.RootElement.GetProperty("steps");

        var hasHeapMetadata = false;
        foreach (var step in stepsElement.EnumerateArray())
        {
            if (step.TryGetProperty("heap", out var heapEl) && heapEl.ValueKind == JsonValueKind.Object)
            {
                hasHeapMetadata = true;
                heapEl.TryGetProperty("phase", out var phaseEl).Should().BeTrue(
                    because: "every Heap metadata object must include a 'phase' field");
                phaseEl.GetString().Should().NotBeNullOrWhiteSpace(
                    because: "the phase field must contain a non-empty value");
                break;
            }
        }

        hasHeapMetadata.Should().BeTrue(
            because: "at least one step must carry non-null Heap metadata");
    }

    [Fact(DisplayName = "BE-IT-HEAP-05 — POST /api/simulation/run returns 400 when array is empty")]
    public async Task Run_Returns400_WhenArrayIsEmpty()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = Array.Empty<int>()
        });

        await AssertValidationBadRequestAsync(response);
    }

    [Fact(DisplayName = "BE-IT-HEAP-06 — POST /api/simulation/run returns 400 with validation error when algorithm field is missing")]
    public async Task Run_Returns400_WhenAlgorithmMissing()
    {
        var response = await _client.PostAsync("/api/simulation/run",
            Json("{ \"array\": [3, 1, 2] }"));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty("Algorithm", out _).Should().BeTrue(
            because: "the validation error must identify the missing 'Algorithm' field");
    }

    [Fact(DisplayName = "BE-IT-HEAP-07 — POST /api/simulation/run returns 401 when request is unauthenticated")]
    public async Task Run_Returns401_WhenUnauthenticated()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-HEAP-08 — POST /api/simulation/run returns 501 for unsupported algorithm variant 'heapsort'")]
    public async Task Run_Returns501_ForUnsupportedAlgorithmVariant()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heapsort",  // no underscore or hyphen separator — not in supported set
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("not supported");
    }

    [Fact(DisplayName = "BE-IT-HEAP-09 — POST /api/simulation/run TotalSteps matches actual Steps count in response")]
    public async Task Run_TotalSteps_MatchesActualStepCount()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = new[] { 2, 8, 4, 6, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.TotalSteps.Should().Be(body.Steps.Count,
            because: "TotalSteps must accurately reflect the number of generated steps");
    }

    [Fact(DisplayName = "BE-IT-HEAP-10 — POST /api/simulation/run ignores target value and sorts normally")]
    public async Task Run_IgnoresTargetValue_AndSortsNormally()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "heap_sort",
            array = new[] { 5, 3, 8, 1 },
            target = 5  // heap sort ignores this; must still sort correctly
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "heap sort ignores any target value and must still produce a sorted result");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/simulation/start — PRACTICE SESSION
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-IT-HEAP-11 — POST /api/simulation/start creates a heap sort practice session with first interactive swap step")]
    public async Task Start_CreatesPracticeSession_ForValidHeapSortInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "heap_sort",
            array = new[] { 3, 1, 4, 1, 5 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();
        session.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().BeLessThan(session.Steps.Count);
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap",
            because: "heap sort's first interactive step is always a swap");
    }

    [Fact(DisplayName = "BE-IT-HEAP-12 — POST /api/simulation/start handles already-sorted ascending input without error")]
    public async Task Start_HandlesAlreadySortedInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "heap_sort",
            array = new[] { 1, 2, 3, 4 }  // ascending order is not a max-heap; heapify runs normally
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "BE-IT-HEAP-13 — POST /api/simulation/start handles already-max-heap input — first interactive step is extraction swap")]
    public async Task Start_HandlesAlreadyMaxHeapInput_FirstInteractiveStepIsExtractionSwap()
    {
        // [9, 5, 7, 2, 3, 1] is a valid max-heap — heapify produces no swaps
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "heap_sort",
            array = new[] { 9, 5, 7, 2, 3, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();

        // No heapify swaps → first interactive step is the root extraction swap
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap",
            because: "skipping heapify swaps, the first interactive step is the extraction root swap");
    }

    [Fact(DisplayName = "BE-IT-HEAP-14 — POST /api/simulation/start returns 400 when array field is missing")]
    public async Task Start_Returns400_WhenArrayIsMissing()
    {
        var response = await _client.PostAsync("/api/simulation/start",
            Json("{ \"algorithm\": \"heap_sort\" }"));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty("Array", out _).Should().BeTrue(
            because: "the validation error must identify the missing 'Array' field");
    }

    [Fact(DisplayName = "BE-IT-HEAP-15 — POST /api/simulation/start returns 401 when request is unauthenticated")]
    public async Task Start_Returns401_WhenUnauthenticated()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "heap_sort",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/simulation/validate-step — INTERACTIVE VALIDATION
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "BE-IT-HEAP-16 — POST /api/simulation/validate-step accepts correct swap and advances session")]
    public async Task ValidateStep_AcceptsCorrectSwap_AndAdvancesSession()
    {
        var session = await StartSessionAsync("heap_sort", [3, 1, 4, 1, 5]);

        var expectedStep = session.Steps[session.CurrentStepIndex];
        expectedStep.ActionLabel.Should().Be("swap");

        var body = await ValidateStepAsync(session.SessionId, "swap", expectedStep.ActiveIndices);

        body.Correct.Should().BeTrue();
        body.NewArrayState.Should().Equal(expectedStep.ArrayState);
        body.CurrentStepIndex.Should().BeGreaterThan(session.CurrentStepIndex);
    }

    [Fact(DisplayName = "BE-IT-HEAP-17 — POST /api/simulation/validate-step rejects incorrect swap without advancing session")]
    public async Task ValidateStep_RejectsIncorrectSwap_WithoutAdvancingSession()
    {
        var session = await StartSessionAsync("heap_sort", [3, 1, 4, 1, 5]);

        var expectedStep = session.Steps[session.CurrentStepIndex];
        expectedStep.ActionLabel.Should().Be("swap");

        // Submit the correct indices in reversed order — fails the SequenceEqual check
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
        body.Hint.Should().Contain("Try swapping index",
            because: "the hint must guide the learner to the correct swap positions");
        body.CurrentStepIndex.Should().Be(session.CurrentStepIndex,
            because: "an incorrect submission must not advance the session");
    }

    [Fact(DisplayName = "BE-IT-HEAP-18 — POST /api/simulation/validate-step returns practice-complete response at terminal state")]
    public async Task ValidateStep_ReturnsPracticeCompleteResponse_AtTerminalState()
    {
        // Single-element heap sort has no swap steps — session starts directly at 'complete'
        var session = await StartSessionAsync("heap_sort", [42]);

        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("complete");

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
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

    [Fact(DisplayName = "BE-IT-HEAP-19 — POST /api/simulation/validate-step returns 404 for unknown session id")]
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

    [Fact(DisplayName = "BE-IT-HEAP-20 — POST /api/simulation/validate-step accepts legacy userAction field alias")]
    public async Task ValidateStep_AcceptsLegacyUserAction_Field()
    {
        var session = await StartSessionAsync("heap_sort", [3, 1, 4, 1, 5]);

        var expectedStep = session.Steps[session.CurrentStepIndex];
        expectedStep.ActionLabel.Should().Be("swap");

        // Use the legacy 'userAction' alias instead of 'action'
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
        body!.Correct.Should().BeTrue(
            because: "the legacy 'userAction' alias must be accepted in place of 'action'");
    }
}
