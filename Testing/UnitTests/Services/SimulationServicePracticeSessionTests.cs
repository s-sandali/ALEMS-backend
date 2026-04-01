using backend.Services;
using backend.Services.Simulations;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Services;

public class SimulationServicePracticeSessionTests
{
    private static SimulationService BuildSut(InMemorySimulationSessionStore store) =>
        new(
            [new BubbleSortSimulationEngine()],
            store,
            NullLogger<SimulationService>.Instance);

    private static SimulationService BuildQuickSortSut(InMemorySimulationSessionStore store) =>
        new(
            [new QuickSortSimulationEngine()],
            store,
            NullLogger<SimulationService>.Instance);

    private static string NormalizeQuickSortAction(string actionLabel) =>
        actionLabel switch
        {
            "pivot_swap" => "swap",
            _ => actionLabel
        };

    [Fact]
    public async Task StartSessionAsync_SeedsSessionAtFirstInteractiveStep()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildSut(store);

        var session = await sut.StartSessionAsync("bubble_sort", [5, 3, 4, 1], null);

        session.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap");
        session.Steps[session.CurrentStepIndex].ActiveIndices.Should().Equal([0, 1]);
    }

    [Fact]
    public async Task ValidateStepAsync_WhenActionMatchesExpectedTrace_AdvancesSession()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildSut(store);
        var session = await sut.StartSessionAsync("bubble_sort", [5, 3, 4, 1], null);

        var response = await sut.ValidateStepAsync(session.SessionId, "swap", [0, 1]);

        response.Correct.Should().BeTrue();
        response.NewArrayState.Should().Equal([3, 5, 4, 1]);
        response.NextExpectedAction.Should().Be("swap");
        response.SuggestedIndices.Should().Equal([1, 2]);

        var updatedSession = store.Get(session.SessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.CurrentStepIndex.Should().Be(response.CurrentStepIndex);
        updatedSession.Steps[updatedSession.CurrentStepIndex].ActiveIndices.Should().Equal([1, 2]);
    }

    [Fact]
    public async Task ValidateStepAsync_WhenActionDoesNotMatchExpectedTrace_DoesNotAdvanceSession()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildSut(store);
        var session = await sut.StartSessionAsync("bubble_sort", [5, 3, 4, 1], null);

        var response = await sut.ValidateStepAsync(session.SessionId, "swap", [1, 2]);

        response.Correct.Should().BeFalse();
        response.NewArrayState.Should().Equal([5, 3, 4, 1]);
        response.NextExpectedAction.Should().Be("swap");
        response.SuggestedIndices.Should().Equal([0, 1]);

        var updatedSession = store.Get(session.SessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
    }

    [Fact]
    public async Task StartSessionAsync_BinarySearchSessionStartsAtMidpointPickStep()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = new SimulationService(
            [new FakeSimulationEngine("binary_search", BuildBinarySearchSteps())],
            store,
            NullLogger<SimulationService>.Instance);

        var session = await sut.StartSessionAsync("binary_search", [7, 11, 15, 21, 29], null);

        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("midpoint_pick");
        session.Steps[session.CurrentStepIndex].ActiveIndices.Should().Equal([2]);
    }

    [Fact]
    public async Task StartSessionAsync_QuickSortSessionStartsAtCompareStep_AndClonesMetadata()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildQuickSortSut(store);

        var session = await sut.StartSessionAsync("quick_sort", [5, 1, 4], null);

        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("compare");
        session.Steps[session.CurrentStepIndex].QuickSort.Should().NotBeNull();
        session.Steps[session.CurrentStepIndex].QuickSort!.Type.Should().Be("compare");
        session.Steps[session.CurrentStepIndex].Recursion.Should().NotBeNull();
        session.Steps[session.CurrentStepIndex].Recursion!.State.Should().Be("compare");

        var storedSession = store.Get(session.SessionId);
        storedSession.Should().NotBeNull();
        storedSession!.Steps[storedSession.CurrentStepIndex].QuickSort.Should().NotBeNull();
        storedSession.Steps[storedSession.CurrentStepIndex].Recursion.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateStepAsync_BinaryMidpointAlias_AdvancesAndExposesTerminalLabel()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = new SimulationService(
            [new FakeSimulationEngine("binary_search", BuildBinarySearchSteps())],
            store,
            NullLogger<SimulationService>.Instance);
        var session = await sut.StartSessionAsync("binary_search", [7, 11, 15, 21, 29], null);

        var response = await sut.ValidateStepAsync(session.SessionId, "midpoint", [2]);

        response.Correct.Should().BeTrue();
        response.NextExpectedAction.Should().Be("target_found");
        response.SuggestedIndices.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateStepAsync_QuickSortPivotSwapAcceptsSwapAlias_WithoutDesynchronizingSession()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildQuickSortSut(store);
        var session = await sut.StartSessionAsync("quick_sort", [5, 1, 4], null);

        var currentIndex = session.CurrentStepIndex;
        while (session.Steps[currentIndex].ActionLabel != "pivot_swap")
        {
            var currentStep = session.Steps[currentIndex];
            var response = await sut.ValidateStepAsync(
                session.SessionId,
                NormalizeQuickSortAction(currentStep.ActionLabel),
                currentStep.ActiveIndices);

            response.Correct.Should().BeTrue();

            currentIndex = response.CurrentStepIndex;
            session = store.Get(session.SessionId)!;
        }

        var pivotSwapStep = session.Steps[currentIndex];
        pivotSwapStep.ActionLabel.Should().Be("pivot_swap");

        var pivotSwapResponse = await sut.ValidateStepAsync(session.SessionId, "swap", pivotSwapStep.ActiveIndices);

        pivotSwapResponse.Correct.Should().BeTrue();
        pivotSwapResponse.CurrentStepIndex.Should().BeGreaterThan(currentIndex);
        pivotSwapResponse.NextExpectedAction.Should().Be("complete");

        var updatedSession = store.Get(session.SessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.CurrentStepIndex.Should().Be(pivotSwapResponse.CurrentStepIndex);
    }

    [Fact]
    public async Task StartSessionAsync_ClonesSearchMetadataForBinarySearchSteps()
    {
        var store = new InMemorySimulationSessionStore();
        var steps = BuildBinarySearchStepsWithSearch();
        var sut = new SimulationService(
            [new FakeSimulationEngine("binary_search", steps)],
            store,
            NullLogger<SimulationService>.Instance);

        var session = await sut.StartSessionAsync("binary_search", [7, 11, 15, 21, 29], null);

        session.Steps.Should().NotBeEmpty();
        session.Steps[0].Search.Should().NotBeNull();
        session.Steps[0].Search!.LowIndex.Should().Be(0);
        session.Steps[0].Search.HighIndex.Should().Be(4);
        session.Steps[0].Search.State.Should().Be("start");
    }

    private static List<backend.Models.Simulations.SimulationStep> BuildBinarySearchSteps() =>
    [
        new()
        {
            StepNumber = 1,
            ArrayState = [7, 11, 15, 21, 29],
            ActiveIndices = [],
            LineNumber = 1,
            ActionLabel = "start"
        },
        new()
        {
            StepNumber = 2,
            ArrayState = [7, 11, 15, 21, 29],
            ActiveIndices = [2],
            LineNumber = 3,
            ActionLabel = "midpoint_pick"
        },
        new()
        {
            StepNumber = 3,
            ArrayState = [7, 11, 15, 21, 29],
            ActiveIndices = [],
            LineNumber = 4,
            ActionLabel = "target_found"
        }
    ];

    private static List<backend.Models.Simulations.SimulationStep> BuildBinarySearchStepsWithSearch() =>
    [
        new()
        {
            StepNumber = 1,
            ArrayState = [7, 11, 15, 21, 29],
            ActiveIndices = [],
            LineNumber = 1,
            ActionLabel = "start",
            Search = new backend.Models.Simulations.SearchStepModel
            {
                LowIndex = 0,
                HighIndex = 4,
                MidpointIndex = null,
                State = "start",
                DiscardedIndices = []
            }
        }
    ];

    private sealed class FakeSimulationEngine : IAlgorithmSimulationEngine
    {
        private readonly string _algorithm;
        private readonly List<backend.Models.Simulations.SimulationStep> _steps;

        public FakeSimulationEngine(string algorithm, List<backend.Models.Simulations.SimulationStep> steps)
        {
            _algorithm = algorithm;
            _steps = steps;
        }

        public bool CanHandle(string algorithm) => algorithm == _algorithm;

        public backend.Models.Simulations.SimulationResponse Run(int[] array, int? targetValue = null)
        {
            return new backend.Models.Simulations.SimulationResponse
            {
                AlgorithmName = "Binary Search",
                Steps = _steps.Select(step => new backend.Models.Simulations.SimulationStep
                {
                    StepNumber = step.StepNumber,
                    ArrayState = step.ArrayState.ToArray(),
                    ActiveIndices = step.ActiveIndices.ToArray(),
                    LineNumber = step.LineNumber,
                    ActionLabel = step.ActionLabel,
                    Search = step.Search is null
                        ? null
                        : new backend.Models.Simulations.SearchStepModel
                        {
                            LowIndex = step.Search.LowIndex,
                            HighIndex = step.Search.HighIndex,
                            MidpointIndex = step.Search.MidpointIndex,
                            State = step.Search.State,
                            DiscardedSide = step.Search.DiscardedSide,
                            DiscardStartIndex = step.Search.DiscardStartIndex,
                            DiscardEndIndex = step.Search.DiscardEndIndex,
                            DiscardedIndices = step.Search.DiscardedIndices.ToArray()
                        }
                }).ToList(),
                TotalSteps = _steps.Count,
                TargetValue = targetValue
            };
        }
    }
}
