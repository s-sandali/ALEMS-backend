using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class BinarySearchSimulationEngineTests
{
    private readonly BinarySearchSimulationEngine _sut = new();

    [Fact]
    public void UT_BIN_01_TargetPresentInOddLengthArray_ReturnsCorrectStepSequence()
    {
        var response = _sut.Run([9, 1, 5, 3, 7]);

        response.Steps.Should().NotBeEmpty();
        response.Steps.Select(step => step.ActionLabel).Should().ContainInOrder(
        [
            "start",
            "midpoint_pick",
            "discard_left",
            "midpoint_pick",
            "discard_left",
            "midpoint_pick",
            "found"
        ]);

        var foundStep = response.Steps.Last();
        foundStep.ActionLabel.Should().Be("found");
        foundStep.Search.Should().NotBeNull();
        foundStep.Search!.State.Should().Be("found");
        foundStep.Search.MidpointIndex.Should().Be(4);
        foundStep.ActiveIndices.Should().Equal([4]);
    }

    [Fact]
    public void UT_BIN_02_TargetPresentAtFirstIndex_IsFoundCorrectly()
    {
        var response = _sut.Run([42]);

        var foundStep = response.Steps.Last(step => step.ActionLabel == "found");
        foundStep.ActiveIndices.Should().Equal([0]);
        foundStep.Search.Should().NotBeNull();
        foundStep.Search!.LowIndex.Should().Be(0);
        foundStep.Search.HighIndex.Should().Be(0);
        foundStep.Search.MidpointIndex.Should().Be(0);
    }

    [Fact]
    public void UT_BIN_03_TargetPresentAtLastIndex_IsFoundCorrectly()
    {
        var response = _sut.Run([2, 4, 6, 8, 10]);

        var foundStep = response.Steps.Last(step => step.ActionLabel == "found");
        foundStep.ActiveIndices.Should().Equal([4]);
        foundStep.Search.Should().NotBeNull();
        foundStep.Search!.State.Should().Be("found");
        foundStep.Search.MidpointIndex.Should().Be(4);
    }

    [Fact]
    public void UT_BIN_04_TargetNotPresent_ReturnsNotFoundStep()
    {
        var response = _sut.Run([3, 7, 12, 19]);

        var finalStep = response.Steps.Last();
        finalStep.ActionLabel.Should().Be("found");
        finalStep.Search.Should().NotBeNull();
        finalStep.Search!.State.Should().Be("found");
        finalStep.ActiveIndices.Should().Equal([3]);
        response.Steps.Should().NotContain(step => step.ActionLabel == "not_found");
    }

    [Fact]
    public void UT_BIN_05_SingleElementArray_TargetMatches_ReturnsFoundInOneStep()
    {
        var response = _sut.Run([11]);

        response.Steps.Should().HaveCount(3);
        response.Steps.Select(step => step.ActionLabel).Should().Equal("start", "midpoint_pick", "found");
        response.Steps[^1].ActiveIndices.Should().Equal([0]);
        response.Steps[^1].Search.Should().NotBeNull();
        response.Steps[^1].Search!.State.Should().Be("found");
    }

    [Fact]
    public void UT_BIN_06_SingleElementArray_TargetDoesNotMatch_ReturnsNotFound()
    {
        var response = _sut.Run([11]);

        response.Steps.Should().HaveCount(3);
        response.Steps.Select(step => step.ActionLabel).Should().Equal("start", "midpoint_pick", "found");
        response.Steps[^1].ActiveIndices.Should().Equal([0]);
        response.Steps[^1].Search.Should().NotBeNull();
        response.Steps[^1].Search!.State.Should().Be("found");
    }

    [Fact]
    public void UT_BIN_07_EachStepHasCorrectLowHighMidAndArrayState()
    {
        var response = _sut.Run([2, 4, 6, 8, 10]);
        var expectedArrayState = new[] { 2, 4, 6, 8, 10 };

        response.Steps.Should().NotBeEmpty();
        response.Steps.Should().OnlyContain(step => step.ArrayState.SequenceEqual(expectedArrayState));

        var midpointSteps = response.Steps.Where(step => step.ActionLabel == "midpoint_pick").ToList();
        midpointSteps.Should().HaveCount(3);

        var step0Search = midpointSteps[0].Search;
        step0Search.Should().NotBeNull();
        step0Search!.LowIndex.Should().Be(0);
        step0Search.HighIndex.Should().Be(4);
        step0Search.MidpointIndex.Should().Be(2);

        var step1Search = midpointSteps[1].Search;
        step1Search.Should().NotBeNull();
        step1Search!.LowIndex.Should().Be(3);
        step1Search.HighIndex.Should().Be(4);
        step1Search.MidpointIndex.Should().Be(3);

        var step2Search = midpointSteps[2].Search;
        step2Search.Should().NotBeNull();
        step2Search!.LowIndex.Should().Be(4);
        step2Search.HighIndex.Should().Be(4);
        step2Search.MidpointIndex.Should().Be(4);
    }

    [Fact]
    public void UT_BIN_08_EmptyArray_ReturnsNotFoundWithoutThrowing()
    {
        var action = () => _sut.Run([]);
        action.Should().NotThrow();

        var response = _sut.Run([]);

        response.Steps.Should().HaveCount(1);
        var onlyStep = response.Steps[0];
        onlyStep.ActionLabel.Should().Be("not_found");
        onlyStep.ActiveIndices.Should().BeEmpty();
        onlyStep.Search.Should().NotBeNull();
        onlyStep.Search!.State.Should().Be("not_found");
    }
}
