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

    [Fact(DisplayName = "UT-IS-24 — InsertionSortEngine: CanHandle with null returns false")]
    public void CanHandle_NullInput_ReturnsFalse()
    {
        _sut.CanHandle(null!).Should().BeFalse();
    }

    [Theory(DisplayName = "UT-IS-25 — InsertionSortEngine: CanHandle with whitespace-only strings returns false")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void CanHandle_WhitespaceOnly_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    // ---- Core action emission ----

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

    // ---- Edge-case inputs ----

    [Fact(DisplayName = "UT-IS-05 — InsertionSortEngine: empty array emits only start and complete")]
    public void Run_EmptyArray_EmitsOnlyStartAndComplete()
    {
        var response = _sut.Run([]);

        response.Steps.Should().HaveCount(2);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[^1].ActionLabel.Should().Be("complete");
        response.Steps.Should().NotContain(s => s.ActionLabel == "select_key");
    }

    [Fact(DisplayName = "UT-IS-06 — InsertionSortEngine: single element emits only start and complete")]
    public void Run_SingleElement_EmitsOnlyStartAndComplete()
    {
        var response = _sut.Run([42]);

        response.Steps.Should().HaveCount(2);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-IS-07 — InsertionSortEngine: two elements needing shift emit one compare, one shift, one insert")]
    public void Run_TwoElementsNeedingShift_EmitsExpectedSteps()
    {
        var response = _sut.Run([2, 1]);

        var labels = response.Steps.Select(s => s.ActionLabel).ToArray();
        labels.Count(l => l == "compare").Should().Be(1);
        labels.Count(l => l == "shift").Should().Be(1);
        labels.Count(l => l == "insert").Should().Be(1);
        response.Steps[^1].ArrayState.Should().Equal(1, 2);
    }

    [Fact(DisplayName = "UT-IS-08 — InsertionSortEngine: already sorted array emits no shift steps")]
    public void Run_AlreadySorted_EmitsNoShiftSteps()
    {
        var response = _sut.Run([1, 2, 3, 4]);

        response.Steps.Should().NotContain(s => s.ActionLabel == "shift");
        response.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 4);
    }

    [Fact(DisplayName = "UT-IS-09 — InsertionSortEngine: reverse sorted array emits maximum number of shifts")]
    public void Run_ReverseSorted_EmitsMaximumShifts()
    {
        // n=4 → n*(n-1)/2 = 6 shifts (worst case)
        var response = _sut.Run([4, 3, 2, 1]);

        response.Steps.Count(s => s.ActionLabel == "shift").Should().Be(6);
        response.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 4);
    }

    [Fact(DisplayName = "UT-IS-10 — InsertionSortEngine: all duplicate values emit no shift steps")]
    public void Run_AllDuplicates_EmitsNoShiftSteps()
    {
        var response = _sut.Run([3, 3, 3]);

        response.Steps.Should().NotContain(s => s.ActionLabel == "shift");
        response.Steps[^1].ArrayState.Should().Equal(3, 3, 3);
    }

    [Fact(DisplayName = "UT-IS-11 — InsertionSortEngine: mixed duplicates produce correctly sorted output")]
    public void Run_WithDuplicates_FinalArrayIsSortedCorrectly()
    {
        var response = _sut.Run([3, 1, 3, 2]);

        response.Steps[^1].ArrayState.Should().Equal(1, 2, 3, 3);
    }

    [Fact(DisplayName = "UT-IS-12 — InsertionSortEngine: negative numbers are sorted ascending")]
    public void Run_NegativeNumbers_FinalArrayIsSortedAscending()
    {
        var response = _sut.Run([-3, -1, -5]);

        response.Steps[^1].ArrayState.Should().Equal(-5, -3, -1);
    }

    [Fact(DisplayName = "UT-IS-13 — InsertionSortEngine: mixed negative and positive numbers are sorted ascending")]
    public void Run_MixedNegativeAndPositive_FinalArrayIsSortedAscending()
    {
        var response = _sut.Run([-2, 5, -1, 0]);

        response.Steps[^1].ArrayState.Should().Equal(-2, -1, 0, 5);
    }

    [Fact(DisplayName = "UT-IS-14 — InsertionSortEngine: array containing zeros is sorted ascending")]
    public void Run_WithZeros_FinalArrayIsSortedAscending()
    {
        var response = _sut.Run([0, 3, 0, -1]);

        response.Steps[^1].ArrayState.Should().Equal(-1, 0, 0, 3);
    }

    // ---- Step structure and metadata ----

    [Fact(DisplayName = "UT-IS-15 — InsertionSortEngine: step numbers are sequential starting from 1")]
    public void Run_StepNumbers_AreSequentialFromOne()
    {
        var response = _sut.Run([5, 3, 8, 4]);

        for (var i = 0; i < response.Steps.Count; i++)
        {
            response.Steps[i].StepNumber.Should().Be(i + 1);
        }
    }

    [Theory(DisplayName = "UT-IS-16 — InsertionSortEngine: first step is always start")]
    [MemberData(nameof(AllInputCases))]
    public void Run_FirstStep_IsAlwaysStart(int[] input)
    {
        _sut.Run(input).Steps[0].ActionLabel.Should().Be("start");
    }

    [Theory(DisplayName = "UT-IS-17 — InsertionSortEngine: last step is always complete")]
    [MemberData(nameof(AllInputCases))]
    public void Run_LastStep_IsAlwaysComplete(int[] input)
    {
        _sut.Run(input).Steps[^1].ActionLabel.Should().Be("complete");
    }

    public static TheoryData<int[]> AllInputCases() => new()
    {
        new int[0],
        new[] { 42 },
        new[] { 3, 1, 2 },
        new[] { 1, 2, 3 },
        new[] { 3, 3, 3 }
    };

    [Fact(DisplayName = "UT-IS-18 — InsertionSortEngine: select_key active indices contains only the current index")]
    public void Run_SelectKeyStep_ActiveIndicesContainsOnlyCurrentIndex()
    {
        var response = _sut.Run([5, 3, 8, 4]);

        var selectKeySteps = response.Steps.Where(s => s.ActionLabel == "select_key").ToArray();
        selectKeySteps.Should().NotBeEmpty();
        selectKeySteps.Should().OnlyContain(s =>
            s.ActiveIndices.Length == 1 &&
            s.InsertionSort != null &&
            s.ActiveIndices[0] == s.InsertionSort.CurrentIndex);
    }

    [Fact(DisplayName = "UT-IS-19 — InsertionSortEngine: insert step reports the correct InsertPosition")]
    public void Run_InsertStep_HasCorrectInsertPosition()
    {
        // [5, 3, 8, 4]: first insert places key=3 at index 0
        var response = _sut.Run([5, 3, 8, 4]);

        var firstInsert = response.Steps.First(s => s.ActionLabel == "insert");
        firstInsert.InsertionSort!.InsertPosition.Should().Be(0);
        firstInsert.ActiveIndices.Should().Equal(0);
    }

    [Fact(DisplayName = "UT-IS-20 — InsertionSortEngine: sorted_boundary step covers indices 0 through current boundary")]
    public void Run_SortedBoundaryStep_CoversCorrectIndexRange()
    {
        var response = _sut.Run([5, 3, 8, 4]);

        var sortedBoundarySteps = response.Steps.Where(s => s.ActionLabel == "sorted_boundary").ToArray();
        sortedBoundarySteps.Should().NotBeEmpty();

        for (var k = 0; k < sortedBoundarySteps.Length; k++)
        {
            var expectedBoundary = k + 1;
            var expectedIndices = Enumerable.Range(0, expectedBoundary + 1).ToArray();
            sortedBoundarySteps[k].InsertionSort!.SortedBoundary.Should().Be(expectedBoundary);
            sortedBoundarySteps[k].ActiveIndices.Should().Equal(expectedIndices);
        }
    }

    [Fact(DisplayName = "UT-IS-21 — InsertionSortEngine: each step's ArrayState is an independent snapshot")]
    public void Run_ArrayState_IsImmutableSnapshot()
    {
        var response = _sut.Run([5, 3, 1]);

        // The start step must capture the original unsorted array
        response.Steps[0].ArrayState.Should().Equal(5, 3, 1);

        // Later steps produce different states
        response.Steps[^1].ArrayState.Should().Equal(1, 3, 5);

        // Verify the start snapshot was not mutated by subsequent steps
        response.Steps[0].ArrayState.Should().Equal(5, 3, 1);
    }

    [Fact(DisplayName = "UT-IS-22 — InsertionSortEngine: start step InsertionSort model has type 'start'")]
    public void Run_StartStep_HasCorrectModelType()
    {
        var response = _sut.Run([3, 1]);

        response.Steps[0].InsertionSort.Should().NotBeNull();
        response.Steps[0].InsertionSort!.Type.Should().Be("start");
    }

    [Fact(DisplayName = "UT-IS-23 — InsertionSortEngine: complete step InsertionSort model has type 'complete'")]
    public void Run_CompleteStep_HasCorrectModelType()
    {
        var response = _sut.Run([3, 1]);

        response.Steps[^1].InsertionSort.Should().NotBeNull();
        response.Steps[^1].InsertionSort!.Type.Should().Be("complete");
    }
}