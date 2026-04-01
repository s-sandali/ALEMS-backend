using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class QuickSortSimulationEngineTests
{
    private readonly QuickSortSimulationEngine _sut = new();

    [Theory]
    [InlineData("quick_sort")]
    [InlineData("quick-sort")]
    [InlineData("  QuIcK_SoRt  ")]
    public void CanHandle_RecognizedQuickSortKeys_ReturnsTrue(string algorithm)
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

    [Fact(DisplayName = "UT-QS-01 - QuickSortEngine: compare step includes pivot and partition range metadata")]
    public void Run_CompareSteps_IncludePivotAndRangeMetadata()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var compareSteps = response.Steps.Where(step => step.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(step =>
            step.QuickSort != null &&
            step.QuickSort.Type == "compare" &&
            step.QuickSort.Pivot.HasValue &&
            step.QuickSort.Range.Length == 2);
    }

    [Fact(DisplayName = "UT-QS-02 - QuickSortEngine: partition emits pivotPlaced with pivot index and final range")]
    public void Run_Partition_EmitsPivotPlacedWithPivotIndexAndRange()
    {
        var response = _sut.Run([9, 4, 7, 2]);

        var pivotPlacedSteps = response.Steps.Where(step => step.ActionLabel == "pivot_placed").ToArray();

        pivotPlacedSteps.Should().NotBeEmpty();
        pivotPlacedSteps.Should().OnlyContain(step =>
            step.QuickSort != null &&
            step.QuickSort.Type == "pivot_placed" &&
            step.QuickSort.PivotIndex.HasValue &&
            step.QuickSort.Range.Length == 2 &&
            step.ActiveIndices.Length == 1 &&
            step.ActiveIndices[0] == step.QuickSort.PivotIndex.Value);
    }

    [Fact(DisplayName = "UT-QS-03 - QuickSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([10, 7, 8, 9, 1, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 5, 7, 8, 9, 10]);
    }
}
