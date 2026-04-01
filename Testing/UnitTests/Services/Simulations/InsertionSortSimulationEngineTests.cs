using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class InsertionSortSimulationEngineTests
{
    private readonly InsertionSortSimulationEngine _sut = new();

    [Theory]
    [InlineData("insertion_sort")]
    [InlineData("insertion-sort")]
    [InlineData("  InSeRtIoN_SoRt  ")]
    public void CanHandle_RecognizedInsertionSortKeys_ReturnsTrue(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("bubble_sort")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    [Fact(DisplayName = "UT-IS-01 - InsertionSortEngine: shift step includes key, compare index, and sorted boundary metadata")]
    public void Run_ShiftStep_ContainsRequiredMetadata()
    {
        var response = _sut.Run([9, 5, 7, 3]);

        var shiftStep = response.Steps.First(step => step.ActionLabel == "shift");

        shiftStep.KeyIndex.Should().HaveValue();
        shiftStep.Key.Should().HaveValue();
        shiftStep.CompareIndex.Should().HaveValue();
        shiftStep.SortedBoundary.Should().HaveValue();

        var keyIndex = shiftStep.KeyIndex!.Value;
        var compareIndex = shiftStep.CompareIndex!.Value;
        var sortedBoundary = shiftStep.SortedBoundary!.Value;

        keyIndex.Should().BeGreaterThan(0);
        compareIndex.Should().BeLessThanOrEqualTo(sortedBoundary);
        sortedBoundary.Should().Be(keyIndex - 1);
    }

    [Fact(DisplayName = "UT-IS-02 - InsertionSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([10, 4, 8, 1, 6]);
        var finalStep = response.Steps[^1];

        finalStep.ArrayState.Should().Equal(1, 4, 6, 8, 10);
        finalStep.ActionLabel.Should().Be("complete");
    }
}
