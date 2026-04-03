using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class SelectionSortSimulationEngineTests
{
    private readonly SelectionSortSimulationEngine _sut = new();

    [Theory]
    [InlineData("selection_sort")]
    [InlineData("selection-sort")]
    [InlineData("  SeLeCtIoN_SoRt  ")]
    public void CanHandle_RecognizedSelectionSortKeys_ReturnsTrue(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("quick_sort")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    [Fact(DisplayName = "UT-SS-02 - SelectionSortEngine: emits all required action types")]
    public void Run_ProducesAllRequiredActions()
    {
        var response = _sut.Run([64, 25, 12, 22, 11]);

        response.Steps.Should().Contain(step => step.ActionLabel == "pass_start");
        response.Steps.Should().Contain(step => step.ActionLabel == "compare");
        response.Steps.Should().Contain(step => step.ActionLabel == "select_min");
        response.Steps.Should().Contain(step => step.ActionLabel == "swap");
        response.Steps.Should().Contain(step => step.ActionLabel == "sorted_boundary");
        response.Steps.Should().Contain(step => step.ActionLabel == "complete");
    }

    [Fact(DisplayName = "UT-SS-03 - SelectionSortEngine: compare and swap steps include metadata")]
    public void Run_CompareAndSwapSteps_IncludeMetadata()
    {
        var response = _sut.Run([64, 25, 12, 22, 11]);

        var compareSteps = response.Steps.Where(step => step.ActionLabel == "compare").ToArray();
        var swapSteps = response.Steps.Where(step => step.ActionLabel == "swap").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(step =>
            step.SelectionSort != null
            && step.SelectionSort.Type == "compare"
            && step.SelectionSort.CurrentIndex.HasValue
            && step.SelectionSort.CandidateIndex.HasValue
            && step.SelectionSort.MinIndex.HasValue);

        swapSteps.Should().NotBeEmpty();
        swapSteps.Should().OnlyContain(step =>
            step.SelectionSort != null
            && step.SelectionSort.Type == "swap"
            && step.SelectionSort.SwapFrom.HasValue
            && step.SelectionSort.SwapTo.HasValue);
    }

    [Fact(DisplayName = "UT-SS-04 - SelectionSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([10, 7, 8, 9, 1, 5]);

        response.Steps[^1].ArrayState.Should().Equal(1, 5, 7, 8, 9, 10);
    }
}
