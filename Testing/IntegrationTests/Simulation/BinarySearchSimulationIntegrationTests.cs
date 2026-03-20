using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using backend.Models.Simulations;
using backend.Services;
using FluentAssertions;
using IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.Simulation;

public class BinarySearchSimulationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BinarySearchSimulationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthHandler.AdminToken);
    }

    [Fact(DisplayName = "BE-IT-BS-01 — POST /api/simulation/run returns binary search trace ending in found")]
    public async Task Run_WhenBinarySearchInputProvided_ReturnsTraceWithMidpointsAndFoundEnding()
    {
        var payload = new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await ParseBodyAsync(response);
        var root = body.RootElement;

        root.GetProperty("algorithmName").GetString().Should().Be("Binary Search");
        var steps = root.GetProperty("steps").EnumerateArray().ToList();
        steps.Should().NotBeEmpty();

        steps.Select(step => step.GetProperty("actionLabel").GetString())
            .Should().Contain("midpoint_pick");

        var lastStep = steps.Last();
        lastStep.GetProperty("actionLabel").GetString().Should().Be("found");
        lastStep.GetProperty("search").GetProperty("state").GetString().Should().Be("found");
    }

    [Fact(DisplayName = "BE-IT-BS-02 — POST /api/simulation/run current contract derives target and ends in found")]
    public async Task Run_CurrentContractWithoutTargetField_EndsInFoundAndNoNotFoundStep()
    {
        var payload = new
        {
            algorithm = "binary_search",
            array = new[] { 3, 7, 12, 19 }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await ParseBodyAsync(response);
        var steps = body.RootElement.GetProperty("steps").EnumerateArray().ToList();

        steps.Last().GetProperty("actionLabel").GetString().Should().Be("found");
        steps.Select(step => step.GetProperty("actionLabel").GetString())
            .Should().NotContain("not_found");
    }

    [Fact(DisplayName = "BE-IT-BS-03 — POST /api/simulation/start starts at first midpoint decision")]
    public async Task Start_WhenBinarySearchSessionCreated_StartsAtFirstMidpointWithExpectedRange()
    {
        var payload = new
        {
            algorithm = "binary_search",
            array = new[] { 1, 3, 5, 7, 9 }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await ParseBodyAsync(response);
        var root = body.RootElement;
        var currentStepIndex = root.GetProperty("currentStepIndex").GetInt32();
        currentStepIndex.Should().Be(1);

        var steps = root.GetProperty("steps").EnumerateArray().ToList();
        var currentStep = steps[currentStepIndex];

        currentStep.GetProperty("actionLabel").GetString().Should().Be("midpoint_pick");
        currentStep.GetProperty("activeIndices").EnumerateArray().Select(x => x.GetInt32())
            .Should().Equal(2);

        var search = currentStep.GetProperty("search");
        search.GetProperty("lowIndex").GetInt32().Should().Be(0);
        search.GetProperty("highIndex").GetInt32().Should().Be(4);
        search.GetProperty("midpointIndex").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "BE-IT-BS-04 — POST /api/simulation/validate-step accepts correct action and advances")]
    public async Task ValidateStep_WhenActionIsCorrect_AdvancesToNextMidpointOrCompletion()
    {
        var sessionId = await StartBinarySearchSessionAsync(new[] { 1, 3, 5, 7, 9 });

        var validatePayload = new
        {
            sessionId,
            action = new
            {
                type = "midpoint_pick",
                indices = new[] { 2 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", validatePayload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Correct.Should().BeTrue();
        result.NextExpectedAction.Should().Be("midpoint_pick");
        result.CurrentStepIndex.Should().Be(3);
    }

    [Fact(DisplayName = "BE-IT-BS-05 — POST /api/simulation/validate-step rejects incorrect action and keeps position")]
    public async Task ValidateStep_WhenActionIsIncorrect_ReturnsFalseAndPreservesSessionPositionWithHint()
    {
        var sessionId = await StartBinarySearchSessionAsync(new[] { 1, 3, 5, 7, 9 });

        var validatePayload = new
        {
            sessionId,
            action = new
            {
                type = "midpoint_pick",
                indices = new[] { 1 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", validatePayload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>(JsonOptions);
        result.Should().NotBeNull();
        result!.Correct.Should().BeFalse();
        result.CurrentStepIndex.Should().Be(1);
        result.NextExpectedAction.Should().Be("midpoint_pick");
        result.Hint.Should().Contain("Pick the midpoint at index 2");
        result.SuggestedIndices.Should().Equal(2);
    }

    [Fact(DisplayName = "BE-IT-BS-06 — Binary search practice completes with found and not-found terminal responses")]
    public async Task ValidateStep_PracticeCompletion_ReturnsExpectedTerminalResponsesForFoundAndNotFound()
    {
        var foundSessionId = await StartBinarySearchSessionAsync(new[] { 1, 3, 5, 7, 9 });

        var first = await ValidateAsync(foundSessionId, "midpoint_pick", new[] { 2 });
        first.Correct.Should().BeTrue();

        var second = await ValidateAsync(foundSessionId, "midpoint_pick", new[] { 3 });
        second.Correct.Should().BeTrue();

        var third = await ValidateAsync(foundSessionId, "midpoint_pick", new[] { 4 });
        third.Correct.Should().BeTrue();
        third.NextExpectedAction.Should().Be("target_found");

        var terminalReplay = await ValidateAsync(foundSessionId, "midpoint_pick", new[] { 4 });
        terminalReplay.Correct.Should().BeFalse();
        terminalReplay.NextExpectedAction.Should().Be("target_found");
        terminalReplay.Message.Should().Be("Practice complete.");

        var notFoundSessionId = "integration_not_found_session";
        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ISimulationSessionStore>();
            store.Save(new SimulationSession
            {
                SessionId = notFoundSessionId,
                CurrentStepIndex = 0,
                Steps =
                [
                    new SimulationStep
                    {
                        StepNumber = 1,
                        ArrayState = new[] { 2, 4, 6 },
                        ActiveIndices = [],
                        ActionLabel = "not_found",
                        LineNumber = 8,
                        Search = new SearchStepModel
                        {
                            LowIndex = 2,
                            HighIndex = 1,
                            MidpointIndex = null,
                            State = "not_found"
                        }
                    }
                ]
            });
        }

        var notFoundTerminal = await ValidateAsync(notFoundSessionId, "midpoint_pick", new[] { 0 });
        notFoundTerminal.Correct.Should().BeFalse();
        notFoundTerminal.NextExpectedAction.Should().Be("target_not_found");
        notFoundTerminal.Message.Should().Be("Practice complete.");
    }

    [Fact(DisplayName = "BE-IT-BS-07 — Binary search request validation rejects missing required inputs")]
    public async Task Run_WhenRequiredInputsAreMissing_Returns400ValidationFailed()
    {
        var payload = new
        {
            algorithm = "binary_search"
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var body = await ParseBodyAsync(response);
        var root = body.RootElement;

        root.GetProperty("statusCode").GetInt32().Should().Be(400);
        root.GetProperty("message").GetString().Should().Be("Validation Failed");
        root.GetProperty("errors").TryGetProperty("Array", out _).Should().BeTrue();
    }

    [Fact(DisplayName = "BE-IT-BS-08 — Binary search enforces sorted-array contract by normalization")]
    public async Task Run_WhenInputArrayIsUnsorted_NormalizesArrayStateConsistently()
    {
        var payload = new
        {
            algorithm = "binary_search",
            array = new[] { 9, 1, 7, 3, 5 }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/run", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await ParseBodyAsync(response);
        var steps = body.RootElement.GetProperty("steps").EnumerateArray().ToList();

        steps.Should().NotBeEmpty();
        var expectedSorted = new[] { 1, 3, 5, 7, 9 };

        foreach (var step in steps)
        {
            step.GetProperty("arrayState").EnumerateArray().Select(x => x.GetInt32())
                .Should().Equal(expectedSorted);
        }
    }

    private async Task<string> StartBinarySearchSessionAsync(int[] array)
    {
        var payload = new
        {
            algorithm = "binary_search",
            array
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/start", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var body = await ParseBodyAsync(response);
        return body.RootElement.GetProperty("sessionId").GetString()!;
    }

    private async Task<SimulationValidationResponse> ValidateAsync(string sessionId, string actionType, int[] indices)
    {
        var payload = new
        {
            sessionId,
            action = new
            {
                type = actionType,
                indices
            }
        };

        var response = await _client.PostAsJsonAsync("/api/simulation/validate-step", payload);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SimulationValidationResponse>(JsonOptions);
        result.Should().NotBeNull();
        return result!;
    }

    private static async Task<JsonDocument> ParseBodyAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrWhiteSpace();
        return JsonDocument.Parse(content);
    }
}
