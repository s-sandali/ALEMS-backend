using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using backend.Models.Simulations;
using backend.Services;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IntegrationTests.Simulation;

public class SelectionSortSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SelectionSortSimulationIntegrationTests(CustomWebApplicationFactory factory)
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

    // ---- POST /api/simulation/run ----

    [Fact(DisplayName = "BE-IT-SS-01 — POST /api/simulation/run returns full selection sort trace for valid unsorted input")]
    public async Task Run_ReturnsFullSelectionSortTrace_ForValidUnsortedInput()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "selection_sort",
            array = new[] { 64, 25, 12, 22, 11 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Selection Sort");
        body.TotalSteps.Should().Be(body.Steps.Count);

        var labels = body.Steps.Select(s => s.ActionLabel).ToArray();
        labels.Should().Contain("pass_start");
        labels.Should().Contain("compare");
        labels.Should().Contain("select_min");
        labels.Should().Contain("swap");
        labels.Should().Contain("sorted_boundary");
        labels.Should().Contain("complete");

        body.Steps[^1].ArrayState.Should().Equal(11, 12, 22, 25, 64);
    }

    [Theory(DisplayName = "BE-IT-SS-02 — POST /api/simulation/run accepts normalized selection sort keys")]
    [InlineData("selection_sort")]
    [InlineData("selection-sort")]
    [InlineData("  SeLeCtIoN_SoRt  ")]
    public async Task Run_AcceptsNormalizedSelectionSortKeys(string algorithm)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm,
            array = new[] { 4, 2, 3, 1 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.Should().NotBeNull();
        body!.AlgorithmName.Should().Be("Selection Sort");
    }

    [Theory(DisplayName = "BE-IT-SS-03 — POST /api/simulation/run validates empty algorithm or empty array")]
    [InlineData("", new[] { 3, 1, 2 })]
    [InlineData("selection_sort", new int[0])]
    public async Task Run_ValidatesEmptyAlgorithmOrArray(string algorithm, int[] array)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm,
            array
        });

        await AssertValidationBadRequestAsync(response);
    }

    [Fact(DisplayName = "BE-IT-SS-04 — POST /api/simulation/run without bearer token returns 401")]
    public async Task Run_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "selection_sort",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-SS-05 — POST /api/simulation/run with invalid bearer token returns 401")]
    public async Task Run_InvalidToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "definitely-invalid-token");

        var response = await client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "selection_sort",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "BE-IT-SS-06 — POST /api/simulation/run with non-admin authenticated user succeeds (no role-based 403)")]
    public async Task Run_UserToken_Succeeds_WithoutForbidden()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.UserToken);

        var response = await client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "selection_sort",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "BE-IT-SS-07 — POST /api/simulation/run returns structured 500 when simulation service throws")]
    public async Task Run_ServiceThrows_ReturnsStructured500()
    {
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISimulationService>();
                services.AddScoped<ISimulationService, ThrowingSimulationService>();
            });
        });

        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);

        var response = await client.PostAsJsonAsync("/api/simulation/run", new
        {
            algorithm = "selection_sort",
            array = new[] { 3, 1, 2 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should()
            .Be("An unexpected error occurred. Please try again later.");
    }

    // ---- POST /api/simulation/start ----

    [Fact(DisplayName = "BE-IT-SS-08 — POST /api/simulation/start creates a selection sort practice session")]
    public async Task Start_CreatesPracticeSession_ForSelectionSort()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "selection_sort",
            array = new[] { 64, 25, 12, 22, 11 }
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var session = await response.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();
        session!.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps.Should().NotBeEmpty();
        session.CurrentStepIndex.Should().BeGreaterThanOrEqualTo(0);
        session.CurrentStepIndex.Should().BeLessThan(session.Steps.Count);
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("compare");
    }

    [Theory(DisplayName = "BE-IT-SS-09 — POST /api/simulation/start validates empty algorithm or empty array")]
    [InlineData("", new[] { 1, 2 })]
    [InlineData("selection_sort", new int[0])]
    public async Task Start_ValidatesEmptyAlgorithmOrArray(string algorithm, int[] array)
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm,
            array
        });

        await AssertValidationBadRequestAsync(response);
    }

    // ---- POST /api/simulation/validate-step ----

    [Fact(DisplayName = "BE-IT-SS-10 — POST /api/simulation/validate-step accepts correct compare and advances session")]
    public async Task ValidateStep_AcceptsCorrectCompare_AndAdvancesSession()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "selection_sort",
            array = new[] { 64, 25, 12, 22, 11 }
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
        body.NextExpectedAction.Should().Be("select_min");
        body.SuggestedIndices.Should().Equal([1]);
    }

    [Fact(DisplayName = "BE-IT-SS-11 — POST /api/simulation/validate-step rejects incorrect select_min index without advancing")]
    public async Task ValidateStep_RejectsIncorrectSelectMin_WithoutAdvancingSession()
    {
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "selection_sort",
            array = new[] { 64, 25, 12, 22, 11 }
        });

        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var compareStep = session!.Steps[session.CurrentStepIndex];
        var compareResponse = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new
            {
                type = "compare",
                indices = compareStep.ActiveIndices
            }
        });
        compareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var wrongSelectMin = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new
            {
                type = "select_min",
                indices = new[] { 2 }
            }
        });

        wrongSelectMin.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await wrongSelectMin.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        body.Should().NotBeNull();
        body!.Correct.Should().BeFalse();
        body.NextExpectedAction.Should().Be("select_min");
        body.SuggestedIndices.Should().Equal([1]);
    }

    [Fact(DisplayName = "BE-IT-SS-12 — POST /api/simulation/validate-step returns 404 for unknown session id")]
    public async Task ValidateStep_Returns404_ForUnknownSessionId()
    {
        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = "missing-session-id",
            action = new
            {
                type = "compare",
                indices = new[] { 0, 1 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var doc = await ParseBodyAsync(response);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("message").GetString().Should().Contain("was not found");
    }

    [Theory(DisplayName = "BE-IT-SS-13 — POST /api/simulation/validate-step validates missing fields and index requirements")]
    [InlineData("{ \"action\": { \"type\": \"compare\", \"indices\": [0,1] } }", "SessionId")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"indices\": [0,1] } }", "Action.Type")]
    [InlineData("{ \"sessionId\": \"abc\", \"action\": { \"type\": \"select_min\", \"indices\": [] } }", "Action.Indices")]
    public async Task ValidateStep_ValidatesMissingFields(string payload, string expectedErrorKey)
    {
        var response = await _client.PostAsync("/api/simulation/validate-step", Json(payload));

        await AssertValidationBadRequestAsync(response);

        using var doc = await ParseBodyAsync(response);
        var errors = doc.RootElement.GetProperty("errors");
        errors.TryGetProperty(expectedErrorKey, out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-SS-14 — Selection sort practice walkthrough keeps state consistent across requests")]
    public async Task PracticeWalkthrough_MaintainsStateConsistency_AcrossRequests()
    {
        // Use a two-element array for a deterministic full flow:
        // compare [0,1] -> select_min [1] -> swap [0,1] -> complete
        var start = await _client.PostAsJsonAsync("/api/simulation/start", new
        {
            algorithm = "selection_sort",
            array = new[] { 2, 1 }
        });

        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await start.Content.ReadFromJsonAsync<SimulationSession>();
        session.Should().NotBeNull();

        var firstStep = session!.Steps[session.CurrentStepIndex];
        firstStep.ActionLabel.Should().Be("compare");
        firstStep.ActiveIndices.Should().Equal([0, 1]);

        var compare = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "compare", indices = new[] { 0, 1 } }
        });
        var compareBody = await compare.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        compareBody.Should().NotBeNull();
        compareBody!.Correct.Should().BeTrue();
        compareBody.NextExpectedAction.Should().Be("select_min");
        compareBody.SuggestedIndices.Should().Equal([1]);

        var selectMin = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "select_min", indices = new[] { 1 } }
        });
        var selectMinBody = await selectMin.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        selectMinBody.Should().NotBeNull();
        selectMinBody!.Correct.Should().BeTrue();
        selectMinBody.NextExpectedAction.Should().Be("swap");
        selectMinBody.SuggestedIndices.Should().Equal([0, 1]);

        var swap = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "swap", indices = new[] { 0, 1 } }
        });
        var swapBody = await swap.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        swapBody.Should().NotBeNull();
        swapBody!.Correct.Should().BeTrue();
        swapBody.NextExpectedAction.Should().Be("complete");
        swapBody.NewArrayState.Should().Equal([1, 2]);

        var terminal = await _client.PostAsJsonAsync("/api/simulation/validate-step", new
        {
            sessionId = session.SessionId,
            action = new { type = "compare", indices = new[] { 0, 1 } }
        });
        var terminalBody = await terminal.Content.ReadFromJsonAsync<SimulationValidationResponse>();
        terminalBody.Should().NotBeNull();
        terminalBody!.Correct.Should().BeFalse();
        terminalBody.NextExpectedAction.Should().Be("complete");
        terminalBody.Message.Should().Be("Practice complete.");
        terminalBody.Hint.Should().Be("No more actions are needed.");
    }

    private sealed class ThrowingSimulationService : ISimulationService
    {
        public Task<SimulationResponse> RunAsync(string algorithm, int[] array, int? targetValue)
            => throw new InvalidOperationException("simulated run failure");

        public Task<SimulationSession> StartSessionAsync(string algorithm, int[] array, int? targetValue)
            => throw new InvalidOperationException("simulated start failure");

        public Task<SimulationValidationResponse> ValidateStepAsync(string sessionId, string actionType, int[] indices)
            => throw new InvalidOperationException("simulated validate failure");
    }
}
