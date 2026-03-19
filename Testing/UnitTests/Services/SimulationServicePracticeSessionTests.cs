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

    [Fact]
    public async Task StartSessionAsync_SeedsSessionAtFirstInteractiveStep()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildSut(store);

        var session = await sut.StartSessionAsync("bubble_sort", [5, 3, 4, 1]);

        session.SessionId.Should().NotBeNullOrWhiteSpace();
        session.Steps[session.CurrentStepIndex].ActionLabel.Should().Be("swap");
        session.Steps[session.CurrentStepIndex].ActiveIndices.Should().Equal([0, 1]);
    }

    [Fact]
    public async Task ValidateStepAsync_WhenActionMatchesExpectedTrace_AdvancesSession()
    {
        var store = new InMemorySimulationSessionStore();
        var sut = BuildSut(store);
        var session = await sut.StartSessionAsync("bubble_sort", [5, 3, 4, 1]);

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
        var session = await sut.StartSessionAsync("bubble_sort", [5, 3, 4, 1]);

        var response = await sut.ValidateStepAsync(session.SessionId, "swap", [1, 2]);

        response.Correct.Should().BeFalse();
        response.NewArrayState.Should().Equal([5, 3, 4, 1]);
        response.NextExpectedAction.Should().Be("swap");
        response.SuggestedIndices.Should().Equal([0, 1]);

        var updatedSession = store.Get(session.SessionId);
        updatedSession.Should().NotBeNull();
        updatedSession!.CurrentStepIndex.Should().Be(session.CurrentStepIndex);
    }
}
