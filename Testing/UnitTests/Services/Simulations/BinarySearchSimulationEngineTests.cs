using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class BinarySearchSimulationEngineTests
{
    private readonly BinarySearchSimulationEngine _sut = new();

    [Fact(DisplayName = "UT-BIN-01 - BinarySearchEngine: target present in odd-length array returns correct step sequence")]
    public void Run_TargetPresentInOddLengthArray_ProducesCorrectMidpointSequence_AndFoundStep()
    {
        var response = _sut.Run([1, 3, 5, 7, 9], 7);

        response.Steps.Should().NotBeEmpty();
        response.Steps.Last().ActionLabel.Should().Be("found");

        var midpointSteps = response.Steps
            .Where(step => step.ActionLabel == "midpoint_pick")
            .ToArray();

        midpointSteps.Should().HaveCount(2);
        midpointSteps[0].Search!.LowIndex.Should().Be(0);
        midpointSteps[0].Search.HighIndex.Should().Be(4);
        midpointSteps[0].Search.MidpointIndex.Should().Be(2);

        midpointSteps[1].Search!.LowIndex.Should().Be(3);
        midpointSteps[1].Search.HighIndex.Should().Be(4);
        midpointSteps[1].Search.MidpointIndex.Should().Be(3);

        var foundStep = response.Steps.Last();
        foundStep.Search.Should().NotBeNull();
        foundStep.Search!.State.Should().Be("found");
        foundStep.Search.MidpointIndex.Should().Be(3);
        foundStep.ActiveIndices.Should().Equal([3]);
    }

    [Fact(DisplayName = "UT-BIN-02 - BinarySearchEngine: target present at first index is found correctly")]
    public void Run_TargetAtFirstIndex_IsFoundCorrectly()
    {
        var response = _sut.Run([10, 20, 30, 40, 50], 10);

        response.Steps.Last().ActionLabel.Should().Be("found");
        response.Steps.Last().Search.Should().NotBeNull();
        response.Steps.Last().Search!.MidpointIndex.Should().Be(0);
        response.Steps.Last().ActiveIndices.Should().Equal([0]);
    }

    [Fact(DisplayName = "UT-BIN-03 - BinarySearchEngine: target present at last index is found correctly")]
    public void Run_TargetAtLastIndex_IsFoundCorrectly()
    {
        var response = _sut.Run([10, 20, 30, 40, 50], 50);

        response.Steps.Last().ActionLabel.Should().Be("found");
        response.Steps.Last().Search.Should().NotBeNull();
        response.Steps.Last().Search!.MidpointIndex.Should().Be(4);
        response.Steps.Last().ActiveIndices.Should().Equal([4]);
    }

    [Fact(DisplayName = "UT-BIN-04 - BinarySearchEngine: target not present returns not-found step")]
    public void Run_TargetNotPresent_EndsWithNotFound_AndNoActiveIndices()
    {
        var response = _sut.Run([1, 3, 5, 7, 9], 8);

        response.Steps.Last().ActionLabel.Should().Be("not_found");
        response.Steps.Last().Search.Should().NotBeNull();
        response.Steps.Last().Search!.State.Should().Be("not_found");
        response.Steps.Last().ActiveIndices.Should().BeEmpty();
    }

    [Fact(DisplayName = "UT-BIN-05 - BinarySearchEngine: single element array, target matches, returns found in one step")]
    public void Run_SingleElementArray_TargetMatches_ReturnsFoundInOneMidpointStep()
    {
        var response = _sut.Run([42], 42);

        response.Steps.Count(step => step.ActionLabel == "midpoint_pick").Should().Be(1);
        response.Steps.Count(step => step.ActionLabel == "found").Should().Be(1);
        response.Steps.Last().ActionLabel.Should().Be("found");
        response.Steps.Last().ActiveIndices.Should().Equal([0]);
    }

    [Fact(DisplayName = "UT-BIN-06 - BinarySearchEngine: single element array, target does not match, returns not-found")]
    public void Run_SingleElementArray_TargetDoesNotMatch_ReturnsNotFound()
    {
        var response = _sut.Run([42], 7);

        response.Steps.Last().ActionLabel.Should().Be("not_found");
        response.Steps.Last().Search.Should().NotBeNull();
        response.Steps.Last().Search!.State.Should().Be("not_found");
        response.Steps.Last().ActiveIndices.Should().BeEmpty();
    }

    [Fact(DisplayName = "UT-BIN-07 - BinarySearchEngine: each step contains correct low, high, mid indices and array_state")]
    public void Run_EachMidpointStep_HasMathematicallyCorrectIndices_AndStableArrayState()
    {
        var response = _sut.Run([1, 3, 5, 7, 9], 9);

        var expectedArrayState = new[] { 1, 3, 5, 7, 9 };

        foreach (var step in response.Steps)
        {
            step.ArrayState.Should().Equal(expectedArrayState);
        }

        var midpointSteps = response.Steps
            .Where(step => step.ActionLabel == "midpoint_pick")
            .ToArray();

        midpointSteps.Should().NotBeEmpty();

        foreach (var step in midpointSteps)
        {
            step.Search.Should().NotBeNull();
            var search = step.Search!;
            var expectedMid = (search.LowIndex + search.HighIndex) / 2;

            search.MidpointIndex.Should().Be(expectedMid);
            search.LowIndex.Should().BeGreaterThanOrEqualTo(0);
            search.HighIndex.Should().BeLessThan(expectedArrayState.Length);
            search.LowIndex.Should().BeLessThanOrEqualTo(search.HighIndex);
        }
    }

    [Fact(DisplayName = "UT-BIN-08 - BinarySearchEngine: empty array returns not-found without throwing")]
    public void Run_EmptyArray_DoesNotThrow_AndReturnsImmediateNotFound()
    {
        var act = () => _sut.Run([], 1);

        act.Should().NotThrow();

        var response = _sut.Run([], 1);

        response.Steps.Should().HaveCount(1);
        response.Steps.Single().ActionLabel.Should().Be("not_found");
        response.Steps.Single().Search.Should().NotBeNull();
        response.Steps.Single().Search!.State.Should().Be("not_found");
        response.Steps.Single().ActiveIndices.Should().BeEmpty();
    }

    [Fact]
    public void Run_AddsSearchMetadataForDiscardSteps()
    {
        var response = _sut.Run([3, 7, 12, 19]);

        var discardStep = response.Steps.First(step => step.ActionLabel == "discard_left");
        discardStep.Search.Should().NotBeNull();
        discardStep.Search!.DiscardedIndices.Should().Equal([0, 1]);
        discardStep.Search.DiscardedSide.Should().Be("left");
        discardStep.Search.State.Should().Be("discard_left");
    }

    [Fact]
    public void Run_AddsSearchMetadataForFoundStep()
    {
        var response = _sut.Run([3, 7, 12, 19]);

        var foundStep = response.Steps.First(step => step.ActionLabel == "found");
        foundStep.Search.Should().NotBeNull();
        foundStep.Search!.State.Should().Be("found");
        foundStep.Search.MidpointIndex.Should().Be(foundStep.ActiveIndices.First());
    }
}
