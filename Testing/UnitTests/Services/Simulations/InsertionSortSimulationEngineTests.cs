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
    [InlineData("quick_sort")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    [Fact(DisplayName = "UT-IS-01 - InsertionSortEngine: emits all required action types")]
    public void Run_ProducesAllRequiredActions()
    {
        var response = _sut.Run([5, 3, 8, 4]);

        response.Steps.Should().Contain(step => step.ActionLabel == "select_key");
        response.Steps.Should().Contain(step => step.ActionLabel == "compare");
        response.Steps.Should().Contain(step => step.ActionLabel == "shift");
        response.Steps.Should().Contain(step => step.ActionLabel == "insert");
        response.Steps.Should().Contain(step => step.ActionLabel == "sorted_boundary");
    }

    [Fact(DisplayName = "UT-IS-02 - InsertionSortEngine: compare and shift steps include key and compare indices")]
    public void Run_CompareAndShiftSteps_IncludeMetadata()
    {
        var response = _sut.Run([5, 3, 8, 4]);

        var compareSteps = response.Steps.Where(step => step.ActionLabel == "compare").ToArray();
        var shiftSteps = response.Steps.Where(step => step.ActionLabel == "shift").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(step =>
            step.InsertionSort != null &&
            step.InsertionSort.Type == "compare" &&
            step.InsertionSort.Key.HasValue &&
            step.InsertionSort.CompareIndex.HasValue);

        shiftSteps.Should().NotBeEmpty();
        shiftSteps.Should().OnlyContain(step =>
            step.InsertionSort != null &&
            step.InsertionSort.Type == "shift" &&
            step.InsertionSort.ShiftFrom.HasValue &&
            step.InsertionSort.ShiftTo.HasValue);
    }

    [Fact(DisplayName = "UT-IS-03 - InsertionSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([10, 7, 8, 9, 1, 5]);

        response.Steps[^1].ArrayState.Should().Equal(1, 5, 7, 8, 9, 10);
    }
}