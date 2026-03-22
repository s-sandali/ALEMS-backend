using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class BinarySearchSimulationEngineTests
{
    [Fact]
    public void Run_AddsSearchMetadataForDiscardSteps()
    {
        var sut = new BinarySearchSimulationEngine();

        var response = sut.Run([3, 7, 12, 19]);

        response.Steps.Should().NotBeEmpty();

        var discardStep = response.Steps.First(step => step.ActionLabel == "discard_left");
        discardStep.Search.Should().NotBeNull();
        discardStep.Search!.DiscardedIndices.Should().Equal([0, 1]);
        discardStep.Search.DiscardedSide.Should().Be("left");
        discardStep.Search.State.Should().Be("discard_left");
    }

    [Fact]
    public void Run_AddsSearchMetadataForFoundStep()
    {
        var sut = new BinarySearchSimulationEngine();

        var response = sut.Run([3, 7, 12, 19]);

        var foundStep = response.Steps.First(step => step.ActionLabel == "found");
        foundStep.Search.Should().NotBeNull();
        foundStep.Search!.State.Should().Be("found");
        foundStep.Search.MidpointIndex.Should().Be(foundStep.ActiveIndices.First());
    }
}
