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

public class InsertionSortSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InsertionSortSimulationIntegrationTests(CustomWebApplicationFactory factory)
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

    // ---- POST /api/simulation/run ----

    [Fact(DisplayName = "BE-IT-IS-02 — POST /api/simulation/run returns full insertion sort trace for valid unsorted input")]
    public async Task Run_ReturnsFullInsertionSortTrace_ForValidUnsortedInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "insertion_sort",
            array = new[] { 5, 2, 4, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Insertion Sort");
        body.TotalSteps.Should().Be(body.Steps.Count);

        var labels = body.Steps.Select(s => s.ActionLabel).ToArray();
        labels.Should().Contain("select_key");
        labels.Should().Contain("compare");
        labels.Should().Contain("shift");
        labels.Should().Contain("insert");
        labels.Should().Contain("complete");

        body.Steps[^1].ArrayState.Should().Equal(1, 2, 4, 5);
    }

    [Theory(DisplayName = "BE-IT-IS-03 — POST /api/simulation/run accepts normalized insertion sort keys")]
    [InlineData("insertion_sort")]
    [InlineData("insertion-sort")]
    [InlineData("  InSeRtIoN_SoRt  ")]
    public async Task Run_AcceptsNormalizedInsertionSortKeys(string algorithm)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm,
            array = new[] { 4, 2, 3, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Insertion Sort");
    }

    [Fact(DisplayName = "BE-IT-IS-04 — POST /api/simulation/run with already sorted input emits no shift steps")]
    public async Task Run_AlreadySortedInput_EmitsNoShiftSteps()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "insertion_sort",
            array = new[] { 1, 2, 3, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.Steps.Should().NotContain(s => s.ActionLabel == "shift");
        body.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 4);
    }

    [Fact(DisplayName = "BE-IT-IS-05 — POST /api/simulation/run with single element returns only start and complete steps")]
    public async Task Run_SingleElement_ReturnsOnlyStartAndComplete()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "insertion_sort",
            array = new[] { 7 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.Steps.Should().HaveCount(2);
        body.Steps[0].ActionLabel.Should().Be("start");
        body.Steps[^1].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "BE-IT-IS-06 — POST /api/simulation/run with reverse sorted input produces correct final array")]
    public async Task Run_ReverseSortedInput_ProducesCorrectFinalArray()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "insertion_sort",
            array = new[] { 4, 3, 2, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        // Worst case: n*(n-1)/2 = 6 shifts
        body!.Steps.Count(s => s.ActionLabel == "shift").Should().Be(6);
        body.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 4);
    }

    [Fact(DisplayName = "BE-IT-IS-07 — POST /api/simulation/run with duplicate values produces correctly sorted output")]
    public async Task Run_DuplicateValues_ProducesCorrectlySortedOutput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "insertion_sort",
            array = new[] { 3, 1, 3, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 3);
        // Equal elements must not be shifted past each other (stable sort):
        // in a shift snapshot, the moved value is at ShiftTo and must be strictly greater than the key
        body.Steps
            .Where(s => s.ActionLabel == "shift")
            .Should().OnlyContain(s =>
                s.InsertionSort != null &&
            s.ArrayState[s.InsertionSort.ShiftTo!.Value] > s.InsertionSort.Key!.Value);
    }

    // ---- POST /api/simulation/start ----

    [Fact(DisplayName = "BE-IT-IS-08 — POST /api/simulation/start creates a practice session for insertion sort")]
    public async Task Start_CreatesPracticeSession_ForInsertionSort()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "insertion_sort",
            array = new[] { 5, 2, 4, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();
        session.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().BeLessThan(session.Steps.Count);

        // First actionable step for an unsorted array is always a compare
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("compare");
    }

    [Fact(DisplayName = "BE-IT-IS-09 — POST /api/simulation/start for already sorted input has no shift steps")]
    public async Task Start_AlreadySortedInput_HasNoShiftSteps()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "insertion_sort",
            array = new[] { 1, 2, 3, 4 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.Steps.Should().NotContain(s => s.ActionLabel == "shift");
        // Insertion sort has no early-exit; the first interactive step is still a compare
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("compare");
    }

    // ---- POST /api/simulation/validate-step ----

    [Fact(DisplayName = "BE-IT-IS-10 — POST /api/simulation/validate-step accepts correct insertion sort compare and advances session")]
    public async Task ValidateStep_AcceptsCorrectCompare_AndAdvancesSession()
    {
        // [3, 1, 2]: first interactive step is compare at indices [0, 1] (3 vs key=1)
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "insertion_sort",
            array = new[] { 3, 1, 2 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var expectedStep = session!.Steps[session.CurrentStepIndex];
        expectedStep.ActionLabel.Should().Be("compare");

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new
            {
                type = "compare",
                indices = expectedStep.ActiveIndices
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeTrue();
        body.CurrentStepIndex.Should().BeGreaterThan(session.CurrentStepIndex);
        // After a compare where the key is less, the next interactive step is a shift
        body.NextExpectedAction.Should().Be("shift");
    }

    [Fact(DisplayName = "BE-IT-IS-11 — POST /api/simulation/validate-step rejects incorrect compare indices without advancing session")]
    public async Task ValidateStep_RejectsIncorrectCompare_WithoutAdvancingSession()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "insertion_sort",
            array = new[] { 3, 1, 2 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var expectedStep = session!.Steps[session.CurrentStepIndex];
        expectedStep.ActionLabel.Should().Be("compare");

        // Submit correct type but wrong index order
        var wrongIndices = expectedStep.ActiveIndices.Reverse().ToArray();
        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new
            {
                type = "compare",
                indices = wrongIndices
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeFalse();
        body.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
        body.Hint.Should().Contain("Compare index");
    }

    [Fact(DisplayName = "BE-IT-IS-12 — POST /api/simulation/validate-step accepts correct shift action and advances session")]
    public async Task ValidateStep_AcceptsCorrectShift_AndAdvancesSession()
    {
        // [3, 1, 2]: first compare (3 > 1) → next step is shift
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "insertion_sort",
            array = new[] { 3, 1, 2 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        // Submit the correct compare first to advance to the shift step
        var compareStep = session!.Steps[session.CurrentStepIndex];
        await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "compare", indices = compareStep.ActiveIndices }
        });

        // Re-fetch the updated session index by submitting again to confirm shift is next
        var shiftResponse = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "shift", indices = compareStep.ActiveIndices } // shift uses same [0,1] indices
        });

        shiftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await shiftResponse.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-IS-13 — POST /api/simulation/validate-step returns hint with shift indices on incorrect action")]
    public async Task ValidateStep_ReturnsShiftHint_WhenExpectedActionIsShift()
    {
        // First submit correct compare to move to the shift step, then submit wrong action
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "insertion_sort",
            array = new[] { 3, 1, 2 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var compareStep = session!.Steps[session.CurrentStepIndex];
        await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "compare", indices = compareStep.ActiveIndices }
        });

        // Now expected is shift; submit wrong indices
        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "shift", indices = new[] { 1, 0 } } // reversed = wrong
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeFalse();
        body.Hint.Should().Contain("Shift the element at index");
    }

    // ---- GET /simulate/insertion-sort ----

    [Fact(DisplayName = "BE-IT-IS-14 — GET /simulate/insertion-sort with no query params uses default array and returns sorted result")]
    public async Task GetInsertionSort_NoQueryParams_UsesDefaultArrayAndReturnsSortedResult()
    {
        var response = await _client.GetAsync("/simulate/insertion-sort");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Insertion Sort");
        // Default input is [5, 2, 4, 6, 1, 3]; sorted result is [1, 2, 3, 4, 5, 6]
        body.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 4, 5, 6);
    }

    [Fact(DisplayName = "BE-IT-IS-15 — GET /simulate/insertion-sort with single element returns valid two-step trace")]
    public async Task GetInsertionSort_SingleElement_ReturnsTwoStepTrace()
    {
        var response = await _client.GetAsync("/simulate/insertion-sort?array=99");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Insertion Sort");
        body.Steps.Should().HaveCount(2);
        body.Steps[0].ActionLabel.Should().Be("start");
        body.Steps[^1].ActionLabel.Should().Be("complete");
    }
}
