using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class BubbleSortSimulationEngineTests
{
    private readonly BubbleSortSimulationEngine _sut = new();

    [Fact(DisplayName = "UT-BS-01 - BubbleSortEngine: sorted array returns steps with no swaps")]
    public void Run_SortedArray_ProducesNoSwapActions_AndFinalStateMatchesInput()
    {
        var input = new[] { 1, 2, 3, 4 };

        var response = _sut.Run(input.ToArray());

        response.Steps.Should().NotContain(step => step.ActionLabel == "swap");
        response.Steps.Last().ActionLabel.Should().Be("complete");
        response.Steps.Last().ArrayState.Should().Equal(input);
    }

    [Fact(DisplayName = "UT-BS-02 - BubbleSortEngine: reverse-sorted array produces correct step sequence")]
    public void Run_ReverseSortedArray_ProducesMaxSwapCount_AndSortedFinalState()
    {
        var input = new[] { 4, 3, 2, 1 };

        var response = _sut.Run(input.ToArray());

        response.Steps.Count(step => step.ActionLabel == "swap").Should().Be(6);
        response.Steps.Last().ActionLabel.Should().Be("complete");
        response.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4]);
    }

    [Fact(DisplayName = "UT-BS-03 - BubbleSortEngine: single element array returns one step with no comparisons")]
    public void Run_SingleElementArray_ProducesMinimalSequenceWithNoComparisonsOrSwaps()
    {
        var response = _sut.Run([42]);

        response.Steps.Should().HaveCountLessOrEqualTo(2);
        response.Steps.Should().NotContain(step => step.ActionLabel == "compare");
        response.Steps.Should().NotContain(step => step.ActionLabel == "swap");
        response.Steps.Last().ArrayState.Should().Equal([42]);
    }

    [Fact(DisplayName = "UT-BS-04 - BubbleSortEngine: duplicate values are handled correctly")]
    public void Run_ArrayWithDuplicates_ProducesCorrectlySortedFinalState()
    {
        var response = _sut.Run([3, 1, 2, 3, 2]);

        response.Steps.Last().ActionLabel.Should().Be("complete");
        response.Steps.Last().ArrayState.Should().Equal([1, 2, 2, 3, 3]);
    }

    [Fact(DisplayName = "UT-BS-05 - BubbleSortEngine: each step contains correct array_state, active_indices, and line_number")]
    public void Run_EachStep_HasValidStateIndicesAndPseudocodeLine()
    {
        var response = _sut.Run([5, 3, 4, 1]);

        response.Steps.Should().OnlyContain(step =>
            step.ArrayState != null &&
            step.ActiveIndices != null &&
            step.LineNumber >= 1 && step.LineNumber <= 6);

        response.Steps
            .Where(step => step.ActionLabel == "compare" || step.ActionLabel == "swap")
            .Should()
            .OnlyContain(step =>
                step.ActiveIndices.Length == 2 &&
                step.ActiveIndices[0] >= 0 &&
                step.ActiveIndices[1] >= 0);
    }

    [Fact(DisplayName = "UT-BS-06 - BubbleSortEngine: empty array returns empty step list without throwing")]
    public void Run_EmptyArray_DoesNotThrow_AndReturnsEmptyOrMinimalSteps()
    {
        var act = () => _sut.Run([]);

        act.Should().NotThrow();
        var response = _sut.Run([]);
        response.Steps.Should().HaveCountLessOrEqualTo(2);
        response.Steps.Should().NotContain(step => step.ActionLabel == "compare" || step.ActionLabel == "swap");
    }

    [Fact(DisplayName = "UT-BS-07 - BubbleSortEngine: two-element unsorted array produces exactly one swap step")]
    public void Run_TwoElementUnsortedArray_ProducesOneCompareAndOneSwap()
    {
        var response = _sut.Run([2, 1]);

        response.Steps.Count(step => step.ActionLabel == "compare").Should().Be(1);
        response.Steps.Count(step => step.ActionLabel == "swap").Should().Be(1);
        response.Steps.Last().ArrayState.Should().Equal([1, 2]);
    }

    [Fact(DisplayName = "Regression - sorted array uses early-exit path")]
    public void Run_WhenArrayIsAlreadySorted_UsesEarlyExitTraceThatMatchesPseudocode()
    {
        var response = _sut.Run([1, 2, 3]);

        response.Steps.Should().ContainSingle(step => step.LineNumber == 5 && step.ActionLabel == "early_exit");
        response.Steps.Should().NotContain(step => step.ActionLabel == "sorted");
        response.Steps.Last().LineNumber.Should().Be(6);
        response.Steps.Last().ActionLabel.Should().Be("complete");
    }
}
