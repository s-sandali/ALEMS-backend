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
        // Act
        _sut.CanHandle(algorithm).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("quick_sort")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string? algorithm)
    {
        // Act
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    [Fact(DisplayName = "UT-SS-01 - SelectionSortEngine: algorithm name is 'Selection Sort'")]
    public void Run_AlgorithmName_IsSelectionSort()
    {
        // Arrange
        var input = new[] { 2, 1 };

        // Act
        var response = _sut.Run(input);

        // Assert
        response.AlgorithmName.Should().Be("Selection Sort");
    }

    [Fact(DisplayName = "UT-SS-02 - SelectionSortEngine: total steps equals list count")]
    public void Run_TotalSteps_MatchesStepsCount()
    {
        // Arrange
        var input = new[] { 64, 25, 12, 22, 11 };

        // Act
        var response = _sut.Run(input);

        // Assert
        response.TotalSteps.Should().Be(response.Steps.Count);
    }

    [Fact(DisplayName = "UT-SS-03 - SelectionSortEngine: first step is start and last step is complete")]
    public void Run_StepSequence_FirstIsStartAndLastIsComplete()
    {
        // Arrange
        var input = new[] { 64, 25, 12 };

        // Act
        var response = _sut.Run(input);

        // Assert
        response.Steps.First().ActionLabel.Should().Be("start");
        response.Steps.Last().ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-SS-04 - SelectionSortEngine: step numbers are consecutive starting from 1")]
    public void Run_StepNumbers_AreConsecutiveStartingFromOne()
    {
        // Arrange
        var input = new[] { 5, 1, 4, 2 };

        // Act
        var response = _sut.Run(input);

        // Assert
        for (var i = 0; i < response.Steps.Count; i++)
        {
            response.Steps[i].StepNumber.Should().Be(i + 1);
        }
    }

    [Fact(DisplayName = "UT-SS-05 - SelectionSortEngine: emits all required action types")]
    public void Run_ProducesAllRequiredActions()
    {
        // Arrange
        var response = _sut.Run([64, 25, 12, 22, 11]);

        // Assert
        response.Steps.Should().Contain(step => step.ActionLabel == "pass_start");
        response.Steps.Should().Contain(step => step.ActionLabel == "compare");
        response.Steps.Should().Contain(step => step.ActionLabel == "select_min");
        response.Steps.Should().Contain(step => step.ActionLabel == "swap");
        response.Steps.Should().Contain(step => step.ActionLabel == "sorted_boundary");
        response.Steps.Should().Contain(step => step.ActionLabel == "complete");
    }

    [Fact(DisplayName = "UT-SS-06 - SelectionSortEngine: compare and swap steps include metadata")]
    public void Run_CompareAndSwapSteps_IncludeMetadata()
    {
        // Arrange
        var response = _sut.Run([64, 25, 12, 22, 11]);

        // Act
        var compareSteps = response.Steps.Where(step => step.ActionLabel == "compare").ToArray();
        var swapSteps = response.Steps.Where(step => step.ActionLabel == "swap").ToArray();

        // Assert
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

    [Fact(DisplayName = "UT-SS-07 - SelectionSortEngine: select_min steps update min index to candidate index")]
    public void Run_SelectMinSteps_MinIndexEqualsCandidateIndex()
    {
        // Arrange
        var response = _sut.Run([64, 25, 12, 22, 11]);

        // Act
        var selectMinSteps = response.Steps.Where(step => step.ActionLabel == "select_min").ToArray();

        // Assert
        selectMinSteps.Should().NotBeEmpty();
        selectMinSteps.Should().OnlyContain(step =>
            step.SelectionSort != null
            && step.SelectionSort.Type == "select_min"
            && step.SelectionSort.MinIndex.HasValue
            && step.SelectionSort.CandidateIndex.HasValue
            && step.SelectionSort.MinIndex.Value == step.SelectionSort.CandidateIndex.Value);
    }

    [Fact(DisplayName = "UT-SS-08 - SelectionSortEngine: sorted boundary steps mark contiguous range from zero")]
    public void Run_SortedBoundarySteps_ExposeContiguousIndicesFromZero()
    {
        // Arrange
        var response = _sut.Run([5, 3, 4, 1]);

        // Act
        var sortedBoundarySteps = response.Steps.Where(step => step.ActionLabel == "sorted_boundary").ToArray();

        // Assert
        sortedBoundarySteps.Should().HaveCount(4);
        for (var i = 0; i < sortedBoundarySteps.Length; i++)
        {
            var expected = Enumerable.Range(0, i + 1).ToArray();
            sortedBoundarySteps[i].ActiveIndices.Should().Equal(expected);
            sortedBoundarySteps[i].SelectionSort!.SortedBoundary.Should().Be(i);
        }
    }

    [Fact(DisplayName = "UT-SS-09 - SelectionSortEngine: line numbers stay in pseudocode range")]
    public void Run_LineNumbers_AreWithinExpectedRange()
    {
        // Arrange
        var response = _sut.Run([5, 3, 4, 1]);

        // Assert
        response.Steps.Should().OnlyContain(step => step.LineNumber >= 1 && step.LineNumber <= 7);
    }

    [Fact(DisplayName = "UT-SS-10 - SelectionSortEngine: all steps include selection metadata with non-empty type")]
    public void Run_AllSteps_IncludeSelectionSortMetadataWithType()
    {
        // Arrange
        var response = _sut.Run([5, 3, 4, 1]);

        // Assert
        response.Steps.Should().OnlyContain(step =>
            step.SelectionSort != null &&
            !string.IsNullOrWhiteSpace(step.SelectionSort.Type));
    }

    [Fact(DisplayName = "UT-SS-11 - SelectionSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        // Arrange
        var response = _sut.Run([10, 7, 8, 9, 1, 5]);

        // Assert
        response.Steps[^1].ArrayState.Should().Equal(1, 5, 7, 8, 9, 10);
    }

    [Fact(DisplayName = "UT-SS-12 - SelectionSortEngine: empty array emits only start and complete")]
    public void Run_EmptyArray_EmitsOnlyStartAndComplete()
    {
        // Act
        var response = _sut.Run([]);

        // Assert
        response.Steps.Should().HaveCount(2);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-SS-13 - SelectionSortEngine: single element emits start sorted_boundary complete")]
    public void Run_SingleElement_EmitsStartSortedBoundaryComplete()
    {
        // Act
        var response = _sut.Run([42]);

        // Assert
        response.Steps.Should().HaveCount(3);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("sorted_boundary");
        response.Steps[2].ActionLabel.Should().Be("complete");
        response.Steps[1].ActiveIndices.Should().Equal([0]);
    }

    [Fact(DisplayName = "UT-SS-14 - SelectionSortEngine: already sorted array emits no swap step")]
    public void Run_AlreadySortedArray_EmitsNoSwapStep()
    {
        // Act
        var response = _sut.Run([1, 2, 3, 4, 5]);

        // Assert
        response.Steps.Should().NotContain(step => step.ActionLabel == "swap");
        response.Steps[^1].ArrayState.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact(DisplayName = "UT-SS-15 - SelectionSortEngine: reverse sorted array ends sorted ascending")]
    public void Run_ReverseSortedArray_FinalStateIsSortedAscending()
    {
        // Act
        var response = _sut.Run([5, 4, 3, 2, 1]);

        // Assert
        response.Steps[^1].ArrayState.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact(DisplayName = "UT-SS-16 - SelectionSortEngine: duplicate values are sorted correctly")]
    public void Run_ArrayWithDuplicates_FinalStateIsSorted()
    {
        // Act
        var response = _sut.Run([3, 1, 3, 2, 1]);

        // Assert
        response.Steps[^1].ArrayState.Should().Equal([1, 1, 2, 3, 3]);
    }

    [Fact(DisplayName = "UT-SS-17 - SelectionSortEngine: mixed negative and positive values are sorted")]
    public void Run_MixedSignValues_FinalStateIsSortedAscending()
    {
        // Act
        var response = _sut.Run([-2, 5, -1, 0, 3]);

        // Assert
        response.Steps[^1].ArrayState.Should().Equal([-2, -1, 0, 3, 5]);
    }

    [Fact(DisplayName = "UT-SS-18 - SelectionSortEngine: Run does not mutate caller input array")]
    public void Run_DoesNotMutateInputArray()
    {
        // Arrange
        var input = new[] { 3, 1, 2 };

        // Act
        _sut.Run(input);

        // Assert
        input.Should().Equal([3, 1, 2]);
    }

    [Fact(DisplayName = "UT-SS-19 - SelectionSortEngine: each step captures independent array snapshot")]
    public void Run_ArrayState_IsIndependentSnapshotPerStep()
    {
        // Act
        var response = _sut.Run([3, 1, 2]);

        // Assert
        var startState = response.Steps.First(step => step.ActionLabel == "start").ArrayState;
        var completeState = response.Steps.Last(step => step.ActionLabel == "complete").ArrayState;

        startState.Should().Equal([3, 1, 2]);
        completeState.Should().Equal([1, 2, 3]);
    }

    [Fact(DisplayName = "UT-SS-20 - SelectionSortEngine: null input throws ArgumentNullException")]
    public void Run_NullArray_ThrowsArgumentNullException()
    {
        // Arrange
        int[] input = null!;

        // Act
        var act = () => _sut.Run(input);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
