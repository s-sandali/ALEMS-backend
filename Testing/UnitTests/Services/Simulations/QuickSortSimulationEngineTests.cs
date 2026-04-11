using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class QuickSortSimulationEngineTests
{
    private readonly QuickSortSimulationEngine _sut = new();

    // ── CanHandle ──────────────────────────────────────────────────────────────

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
    [InlineData("binary_search")]
    [InlineData("   ")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    // ── Sorting Correctness ────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-QS-01 - QuickSortEngine: compare step includes pivot and partition range metadata")]
    public void Run_CompareSteps_IncludePivotAndRangeMetadata()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var compareSteps = response.Steps.Where(s => s.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.Type == "compare" &&
            s.QuickSort.Pivot.HasValue &&
            s.QuickSort.Range.Length == 2);
    }

    [Fact(DisplayName = "UT-QS-02 - QuickSortEngine: partition emits pivot_placed with pivot index and final range")]
    public void Run_Partition_EmitsPivotPlacedWithPivotIndexAndRange()
    {
        var response = _sut.Run([9, 4, 7, 2]);

        var pivotPlacedSteps = response.Steps.Where(s => s.ActionLabel == "pivot_placed").ToArray();

        pivotPlacedSteps.Should().NotBeEmpty();
        pivotPlacedSteps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.Type == "pivot_placed" &&
            s.QuickSort.PivotIndex.HasValue &&
            s.QuickSort.Range.Length == 2 &&
            s.ActiveIndices.Length == 1 &&
            s.ActiveIndices[0] == s.QuickSort.PivotIndex.Value);
    }

    [Fact(DisplayName = "UT-QS-03 - QuickSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([10, 7, 8, 9, 1, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 5, 7, 8, 9, 10]);
    }

    [Fact(DisplayName = "UT-QS-04 - QuickSortEngine: already sorted array preserves correct order in final state")]
    public void Run_AlreadySortedArray_FinalStateMatchesInput()
    {
        var response = _sut.Run([1, 2, 3, 4, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact(DisplayName = "UT-QS-05 - QuickSortEngine: reverse sorted array produces correctly sorted final state")]
    public void Run_ReverseSortedArray_FinalStateIsSortedAscending()
    {
        var response = _sut.Run([5, 4, 3, 2, 1]);

        response.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact(DisplayName = "UT-QS-06 - QuickSortEngine: array with duplicate values produces correctly sorted final state")]
    public void Run_ArrayWithDuplicates_FinalStateIsSortedAscending()
    {
        var response = _sut.Run([3, 1, 4, 1, 5, 9, 2, 6, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 1, 2, 3, 4, 5, 5, 6, 9]);
    }

    [Fact(DisplayName = "UT-QS-07 - QuickSortEngine: single element array leaves array state unchanged")]
    public void Run_SingleElementArray_ArrayStateUnchanged()
    {
        var response = _sut.Run([42]);

        response.Steps.Last().ArrayState.Should().Equal([42]);
    }

    // ── Step Sequence ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-QS-08 - QuickSortEngine: first step is 'start' and last step is 'complete'")]
    public void Run_StepSequence_FirstIsStartAndLastIsComplete()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        response.Steps.First().ActionLabel.Should().Be("start");
        response.Steps.Last().ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-QS-09 - QuickSortEngine: StepNumber starts at 1 and increments by 1 with no gaps")]
    public void Run_StepNumbers_AreConsecutiveStartingFromOne()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var stepNumbers = response.Steps.Select(s => s.StepNumber).ToArray();

        stepNumbers.First().Should().Be(1);
        for (var i = 1; i < stepNumbers.Length; i++)
        {
            stepNumbers[i].Should().Be(stepNumbers[i - 1] + 1,
                because: $"step at position {i + 1} should immediately follow step {i}");
        }
    }

    [Fact(DisplayName = "UT-QS-10 - QuickSortEngine: TotalSteps matches the actual count of steps in the list")]
    public void Run_TotalSteps_MatchesStepsCount()
    {
        var response = _sut.Run([6, 3, 8, 2, 9]);

        response.TotalSteps.Should().Be(response.Steps.Count);
    }

    [Fact(DisplayName = "UT-QS-11 - QuickSortEngine: AlgorithmName is 'Quick Sort'")]
    public void Run_AlgorithmName_IsQuickSort()
    {
        var response = _sut.Run([1, 2]);

        response.AlgorithmName.Should().Be("Quick Sort");
    }

    [Fact(DisplayName = "UT-QS-12 - QuickSortEngine: empty array emits only start and complete steps without throwing")]
    public void Run_EmptyArray_EmitsOnlyStartAndCompleteWithoutThrowing()
    {
        var act = () => _sut.Run([]);
        act.Should().NotThrow();

        var response = _sut.Run([]);

        response.Steps.Should().HaveCount(2);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-QS-13 - QuickSortEngine: single element array emits exactly start, recursive_call, base_case, complete")]
    public void Run_SingleElementArray_EmitsExactlyFourStepsInCorrectOrder()
    {
        var response = _sut.Run([7]);

        response.Steps.Should().HaveCount(4);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("recursive_call");
        response.Steps[2].ActionLabel.Should().Be("base_case");
        response.Steps[3].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-QS-14 - QuickSortEngine: ArrayState length matches input length on every step")]
    public void Run_AllSteps_ArrayStateLengthMatchesInput()
    {
        var input = new[] { 5, 3, 8, 1, 2 };
        var response = _sut.Run(input);

        response.Steps.Should().OnlyContain(s => s.ArrayState.Length == input.Length);
    }

    [Fact(DisplayName = "UT-QS-15 - QuickSortEngine: ArrayState is a deep-copy snapshot captured at each step")]
    public void Run_ArrayState_IsSnapshotedIndependentlyPerStep()
    {
        // If steps shared the same reference, both start and complete would show [1, 2, 3]
        var response = _sut.Run([3, 1, 2]);

        var startState = response.Steps.First(s => s.ActionLabel == "start").ArrayState;
        var completeState = response.Steps.Last(s => s.ActionLabel == "complete").ArrayState;

        startState.Should().Equal([3, 1, 2]);
        completeState.Should().Equal([1, 2, 3]);
    }

    // ── QuickSort Metadata ─────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-QS-16 - QuickSortEngine: every step has a non-null QuickSort metadata model")]
    public void Run_AllSteps_HaveNonNullQuickSortModel()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s => s.QuickSort != null);
    }

    [Fact(DisplayName = "UT-QS-17 - QuickSortEngine: all QuickSort.Type values use snake_case with no uppercase letters")]
    public void Run_AllQuickSortTypes_AreSnakeCase()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        response.Steps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.Type == s.QuickSort.Type.ToLowerInvariant(),
            because: "QuickSort.Type must match the snake_case convention");
    }

    [Fact(DisplayName = "UT-QS-18 - QuickSortEngine: pivot_select step carries pivot value, pivot index, range, and single active index")]
    public void Run_PivotSelectSteps_HaveFullPivotMetadataAndSingleActiveIndex()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var pivotSelectSteps = response.Steps.Where(s => s.ActionLabel == "pivot_select").ToArray();

        pivotSelectSteps.Should().NotBeEmpty();
        pivotSelectSteps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.Pivot.HasValue &&
            s.QuickSort.PivotIndex.HasValue &&
            s.QuickSort.Range.Length == 2 &&
            s.ActiveIndices.Length == 1 &&
            s.ActiveIndices[0] == s.QuickSort.PivotIndex.Value);
    }

    [Fact(DisplayName = "UT-QS-19 - QuickSortEngine: pivot_positioned step reports the pivot's final index within its partition range")]
    public void Run_PivotPositionedSteps_PivotIndexFallsWithinRange()
    {
        var response = _sut.Run([9, 4, 7, 2]);

        var pivotPositionedSteps = response.Steps.Where(s => s.ActionLabel == "pivot_positioned").ToArray();

        pivotPositionedSteps.Should().NotBeEmpty();
        pivotPositionedSteps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.PivotIndex.HasValue &&
            s.QuickSort.Range.Length == 2 &&
            s.QuickSort.PivotIndex.Value >= s.QuickSort.Range[0] &&
            s.QuickSort.PivotIndex.Value <= s.QuickSort.Range[1]);
    }

    [Fact(DisplayName = "UT-QS-20 - QuickSortEngine: RecursionDepth is non-negative on every step")]
    public void Run_AllSteps_RecursionDepthIsNonNegative()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.RecursionDepth.HasValue &&
            s.QuickSort.RecursionDepth.Value >= 0);
    }

    [Fact(DisplayName = "UT-QS-21 - QuickSortEngine: RecursionDepth exceeds 0 in at least one step for a multi-element array")]
    public void Run_MultiElementArray_RecursionDepthExceedsZeroInNestedCalls()
    {
        var response = _sut.Run([4, 2, 6, 1, 5]);

        var maxDepth = response.Steps
            .Where(s => s.QuickSort?.RecursionDepth.HasValue == true)
            .Max(s => s.QuickSort!.RecursionDepth!.Value);

        maxDepth.Should().BeGreaterThan(0,
            because: "sorting a multi-element array must recurse into at least one sub-partition");
    }

    // ── Recursion Model ────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-QS-22 - QuickSortEngine: every step has a non-null Recursion metadata model")]
    public void Run_AllSteps_HaveNonNullRecursionModel()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s => s.Recursion != null);
    }

    [Fact(DisplayName = "UT-QS-23 - QuickSortEngine: 'start' step has an empty recursion stack and no current frame")]
    public void Run_StartStep_HasEmptyRecursionStackAndNoCurrentFrame()
    {
        var response = _sut.Run([4, 2, 7]);

        var startStep = response.Steps.First(s => s.ActionLabel == "start");

        startStep.Recursion.Should().NotBeNull();
        startStep.Recursion!.Stack.Should().BeEmpty();
        startStep.Recursion.CurrentFrameId.Should().BeNull();
    }

    [Fact(DisplayName = "UT-QS-24 - QuickSortEngine: 'complete' step has an empty recursion stack and no current frame")]
    public void Run_CompleteStep_HasEmptyRecursionStackAndNoCurrentFrame()
    {
        var response = _sut.Run([4, 2, 7]);

        var completeStep = response.Steps.Last(s => s.ActionLabel == "complete");

        completeStep.Recursion.Should().NotBeNull();
        completeStep.Recursion!.Stack.Should().BeEmpty();
        completeStep.Recursion.CurrentFrameId.Should().BeNull();
    }

    [Fact(DisplayName = "UT-QS-25 - QuickSortEngine: recursive_call step pushes a new frame onto the stack")]
    public void Run_RecursiveCallSteps_HaveNonEmptyStackAndCurrentFrameId()
    {
        var response = _sut.Run([3, 1, 2]);

        var recursiveCallSteps = response.Steps.Where(s => s.ActionLabel == "recursive_call").ToArray();

        recursiveCallSteps.Should().NotBeEmpty();
        recursiveCallSteps.Should().OnlyContain(s =>
            s.Recursion != null &&
            s.Recursion.Stack.Count > 0 &&
            s.Recursion.CurrentFrameId.HasValue);
    }

    [Fact(DisplayName = "UT-QS-26 - QuickSortEngine: base_case step records Recursion.State as 'return'")]
    public void Run_BaseCaseSteps_HaveRecursionStateReturn()
    {
        var response = _sut.Run([3, 1, 2]);

        var baseCaseSteps = response.Steps.Where(s => s.ActionLabel == "base_case").ToArray();

        baseCaseSteps.Should().NotBeEmpty();
        baseCaseSteps.Should().OnlyContain(s =>
            s.Recursion != null &&
            s.Recursion.State == "return");
    }

    [Fact(DisplayName = "UT-QS-27 - QuickSortEngine: nested recursive calls produce stack snapshots larger than one frame")]
    public void Run_NestedCalls_StackSnapshotGrowsBeyondOneFrame()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var maxStackSize = response.Steps
            .Where(s => s.ActionLabel == "recursive_call")
            .Max(s => s.Recursion!.Stack.Count);

        maxStackSize.Should().BeGreaterThan(1,
            because: "sorting four elements requires at least two levels of recursion");
    }

    [Fact(DisplayName = "UT-QS-28 - QuickSortEngine: recursion frame Ids are unique integers starting from 1")]
    public void Run_RecursionFrameIds_AreUniqueAndStartFromOne()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var allFrameIds = response.Steps
            .Where(s => s.Recursion != null)
            .SelectMany(s => s.Recursion!.Stack.Select(f => f.Id))
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        allFrameIds.Should().NotBeEmpty();
        allFrameIds.First().Should().Be(1, because: "frame IDs are assigned starting from 1");

        for (var i = 1; i < allFrameIds.Length; i++)
        {
            allFrameIds[i].Should().BeGreaterThan(allFrameIds[i - 1],
                because: "each new frame must receive a strictly higher ID");
        }
    }

    [Fact(DisplayName = "UT-QS-29 - QuickSortEngine: single-element sub-partitions recurse and emit a base_case step")]
    public void Run_SingleElementPartitions_EmitBaseCaseStepForVisualizationCompleteness()
    {
        // [5, 3]: after partitioning, pivot (3) lands at index 0
        // The right sub-partition [5] is a single element and must recurse to show base_case
        var response = _sut.Run([5, 3]);

        response.Steps.Should().Contain(s => s.ActionLabel == "base_case",
            because: "single-element sub-partitions must reach base_case so learners see the termination condition");
    }

    // ── Action Labels ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-QS-30 - QuickSortEngine: all ActionLabel values are snake_case with no uppercase letters")]
    public void Run_AllActionLabels_AreSnakeCase()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s =>
            s.ActionLabel == s.ActionLabel.ToLowerInvariant(),
            because: "ActionLabel must follow snake_case — no uppercase characters are permitted");
    }

    [Fact(DisplayName = "UT-QS-31 - QuickSortEngine: 'start' and 'complete' each appear exactly once")]
    public void Run_StartAndComplete_EachAppearExactlyOnce()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        response.Steps.Count(s => s.ActionLabel == "start").Should().Be(1);
        response.Steps.Count(s => s.ActionLabel == "complete").Should().Be(1);
    }

    [Fact(DisplayName = "UT-QS-32 - QuickSortEngine: all structurally mandatory action labels appear for a multi-element sort")]
    public void Run_MultiElementArray_ContainsAllMandatoryActionLabels()
    {
        var response = _sut.Run([5, 3, 8, 1, 6]);

        var labels = response.Steps.Select(s => s.ActionLabel).ToHashSet();

        labels.Should().Contain("start");
        labels.Should().Contain("recursive_call");
        labels.Should().Contain("base_case");
        labels.Should().Contain("partition_start");
        labels.Should().Contain("pivot_select");
        labels.Should().Contain("compare");
        labels.Should().Contain("pivot_placed");
        labels.Should().Contain("pivot_positioned");
        labels.Should().Contain("return");
        labels.Should().Contain("complete");
    }

    [Fact(DisplayName = "UT-QS-33 - QuickSortEngine: sort_left_start and sort_right_start are emitted when both sub-partitions exist")]
    public void Run_ArrayWithBothSubPartitions_EmitsSortLeftAndSortRightSteps()
    {
        // [5, 3, 8, 1]: after the first partition pivot lands at 0 (only right sub-partition),
        // the nested partition then produces both left and right sub-partitions
        var response = _sut.Run([5, 3, 8, 1]);

        var labels = response.Steps.Select(s => s.ActionLabel).ToHashSet();

        labels.Should().Contain("sort_left_start",
            because: "a nested partition produces a left sub-partition with at least one element");
        labels.Should().Contain("sort_right_start",
            because: "a nested partition produces a right sub-partition with at least one element");
    }

    // ── Active Indices & Structural Consistency ────────────────────────────────

    [Fact(DisplayName = "UT-QS-34 - QuickSortEngine: all ActiveIndices are within array bounds on every step")]
    public void Run_AllActiveIndices_AreWithinArrayBounds()
    {
        var input = new[] { 5, 3, 8, 1, 2 };
        var response = _sut.Run(input);

        foreach (var step in response.Steps.Where(s => s.ActiveIndices.Length > 0))
        {
            step.ActiveIndices.Should().OnlyContain(idx =>
                idx >= 0 && idx < input.Length,
                because: $"step '{step.ActionLabel}' must not reference an out-of-bounds index");
        }
    }

    [Fact(DisplayName = "UT-QS-35 - QuickSortEngine: compare steps highlight exactly two indices — the candidate and the pivot position")]
    public void Run_CompareSteps_HaveExactlyTwoActiveIndices()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var compareSteps = response.Steps.Where(s => s.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(s => s.ActiveIndices.Length == 2,
            because: "each compare step marks the current element (j) and the pivot position (high)");
    }

    [Fact(DisplayName = "UT-QS-36 - QuickSortEngine: partition Range bounds are valid — low >= 0, high < array length, low <= high")]
    public void Run_PartitionSteps_RangeBoundsAreWithinArrayAndOrdered()
    {
        var input = new[] { 4, 2, 7, 1, 5 };
        var response = _sut.Run(input);
        var arrayLength = input.Length;

        var partitionSteps = response.Steps
            .Where(s => s.ActionLabel is "partition_start" or "pivot_select" or "compare" or "pivot_placed")
            .ToArray();

        partitionSteps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.Range.Length == 2 &&
            s.QuickSort.Range[0] >= 0 &&
            s.QuickSort.Range[1] < arrayLength &&
            s.QuickSort.Range[0] <= s.QuickSort.Range[1]);
    }

    [Fact(DisplayName = "UT-QS-37 - QuickSortEngine: Run does not mutate the caller's original input array")]
    public void Run_DoesNotMutateOriginalInputArray()
    {
        var input = new[] { 4, 1, 3, 2 };
        var original = input.ToArray();

        var response = _sut.Run(input);

        input.Should().Equal(original,
            because: "the engine should sort a cloned working array, not the caller's input buffer");
        response.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4]);
    }

    [Fact(DisplayName = "UT-QS-38 - QuickSortEngine: mixed signed values are sorted correctly")]
    public void Run_MixedSignedValues_FinalStateIsSortedAscending()
    {
        var response = _sut.Run([0, -5, 8, -1, 3]);

        response.Steps.Last().ArrayState.Should().Equal([-5, -1, 0, 3, 8]);
    }

    [Fact(DisplayName = "UT-QS-39 - QuickSortEngine: int.MinValue and int.MaxValue are sorted correctly")]
    public void Run_ExtremeIntegerValues_FinalStateIsSortedAscending()
    {
        var response = _sut.Run([int.MaxValue, 0, int.MinValue, 42, -7]);

        response.Steps.Last().ArrayState.Should().Equal([int.MinValue, -7, 0, 42, int.MaxValue]);
    }

    [Fact(DisplayName = "UT-QS-40 - QuickSortEngine: all-equal arrays preserve values through to the final state")]
    public void Run_AllEqualValues_FinalStateMatchesInput()
    {
        var response = _sut.Run([4, 4, 4, 4]);

        response.Steps.Last().ArrayState.Should().Equal([4, 4, 4, 4]);
    }

    [Fact(DisplayName = "UT-QS-41 - QuickSortEngine: two-element sorted array does not emit a pivot_swap step")]
    public void Run_TwoElementSortedArray_DoesNotEmitPivotSwap()
    {
        var response = _sut.Run([1, 2]);

        response.Steps.Last().ArrayState.Should().Equal([1, 2]);
        response.Steps.Should().NotContain(s => s.ActionLabel == "pivot_swap",
            because: "when the pivot is already at its final position there is nothing to swap");
        response.Steps.Should().ContainSingle(s =>
            s.ActionLabel == "pivot_placed" &&
            s.QuickSort != null &&
            s.QuickSort.PivotIndex == 1);
    }

    [Fact(DisplayName = "UT-QS-42 - QuickSortEngine: two-element reversed array emits a pivot_swap and sorts correctly")]
    public void Run_TwoElementReversedArray_EmitsPivotSwapAndSorts()
    {
        var response = _sut.Run([2, 1]);

        response.Steps.Last().ArrayState.Should().Equal([1, 2]);

        var pivotSwapStep = response.Steps.Single(s => s.ActionLabel == "pivot_swap");
        pivotSwapStep.ActiveIndices.Should().Equal([0, 1]);
        pivotSwapStep.QuickSort.Should().NotBeNull();
        pivotSwapStep.QuickSort!.Type.Should().Be("pivot_swap");
        pivotSwapStep.QuickSort.Pivot.Should().Be(1);
        pivotSwapStep.QuickSort.Range.Should().Equal([0, 1]);
    }

    [Fact(DisplayName = "UT-QS-43 - QuickSortEngine: internal swap steps expose swap metadata and the swapped indices directly")]
    public void Run_InternalSwapSteps_HaveExpectedMetadataAndActiveIndices()
    {
        var response = _sut.Run([4, 1, 3, 2]);

        var swapStep = response.Steps.Single(s => s.ActionLabel == "swap");

        swapStep.ActiveIndices.Should().Equal([0, 1]);
        swapStep.ArrayState.Should().Equal([1, 4, 3, 2]);
        swapStep.QuickSort.Should().NotBeNull();
        swapStep.QuickSort!.Type.Should().Be("swap");
        swapStep.QuickSort.Pivot.Should().Be(2);
        swapStep.QuickSort.PivotIndex.Should().BeNull();
        swapStep.QuickSort.Range.Should().Equal([0, 3]);
    }

    [Fact(DisplayName = "UT-QS-44 - QuickSortEngine: compare steps always highlight the candidate index plus the pivot index at high")]
    public void Run_CompareSteps_SecondActiveIndexAlwaysMatchesPivotIndexAtHigh()
    {
        var response = _sut.Run([4, 1, 3, 2]);

        var compareSteps = response.Steps.Where(s => s.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(s =>
            s.QuickSort != null &&
            s.QuickSort.PivotIndex.HasValue &&
            s.QuickSort.Range.Length == 2 &&
            s.ActiveIndices.Length == 2 &&
            s.ActiveIndices[1] == s.QuickSort.PivotIndex.Value &&
            s.QuickSort.PivotIndex.Value == s.QuickSort.Range[1]);
    }

    [Fact(DisplayName = "UT-QS-45 - QuickSortEngine: pivot value remains consistent across all pivot-carrying steps within a partition")]
    public void Run_PartitionSteps_KeepPivotValueConsistentWithinEachPartition()
    {
        var response = _sut.Run([5, 1, 4, 2, 3]);

        var pivotSelectSteps = response.Steps.Where(s => s.ActionLabel == "pivot_select").ToArray();
        pivotSelectSteps.Should().NotBeEmpty();

        foreach (var pivotSelectStep in pivotSelectSteps)
        {
            var partitionRange = pivotSelectStep.QuickSort!.Range;
            var partitionDepth = pivotSelectStep.QuickSort.RecursionDepth;
            var pivotValue = pivotSelectStep.QuickSort.Pivot;

            var partitionSteps = response.Steps
                .Where(s =>
                    s.QuickSort != null &&
                    s.QuickSort.RecursionDepth == partitionDepth &&
                    s.QuickSort.Range.SequenceEqual(partitionRange) &&
                    s.ActionLabel is "pivot_select" or "compare" or "swap" or "pivot_swap" or "pivot_placed")
                .ToArray();

            partitionSteps.Should().NotBeEmpty();
            partitionSteps.Should().OnlyContain(s => s.QuickSort!.Pivot == pivotValue,
                because: "every partition step should describe the same pivot value until that partition finishes");
        }
    }

    [Fact(DisplayName = "UT-QS-46 - QuickSortEngine: recursion frames preserve call details and restore the parent frame after nested returns")]
    public void Run_RecursionFrames_PreserveCallDetailsAndRestoreParentAfterNestedReturn()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var nestedReturnStep = response.Steps.First(s =>
            s.ActionLabel == "return" &&
            s.Recursion != null &&
            s.Recursion.Stack.Count > 1);

        nestedReturnStep.Recursion.Should().NotBeNull();
        nestedReturnStep.Recursion!.CurrentFrameId.Should().Be(nestedReturnStep.Recursion.Stack.Last().Id);

        var parentFrame = nestedReturnStep.Recursion.Stack[^2];
        var childFrame = nestedReturnStep.Recursion.Stack[^1];

        parentFrame.FunctionName.Should().Be("quickSort");
        parentFrame.LeftIndex.Should().Be(0);
        parentFrame.RightIndex.Should().Be(3);

        childFrame.FunctionName.Should().Be("quickSort");
        childFrame.LeftIndex.Should().Be(nestedReturnStep.QuickSort!.Range[0]);
        childFrame.RightIndex.Should().Be(nestedReturnStep.QuickSort.Range[1]);
        childFrame.ReturnValue.Should().NotBeNullOrWhiteSpace();

        var parentRestoredStep = response.Steps.First(s =>
            s.ActionLabel is "sort_left_complete" or "sort_right_complete" &&
            s.Recursion != null &&
            s.Recursion.Stack.Count == 1);

        parentRestoredStep.Recursion!.CurrentFrameId.Should().Be(parentRestoredStep.Recursion.Stack.Single().Id);

        var restoredParent = parentRestoredStep.Recursion.Stack.Single();
        restoredParent.FunctionName.Should().Be("quickSort");
        restoredParent.LeftIndex.Should().Be(0);
        restoredParent.RightIndex.Should().Be(3);
    }

    [Fact(DisplayName = "UT-QS-47 - QuickSortEngine: key partition actions map to the expected pseudocode line numbers")]
    public void Run_KeyPartitionActions_MapToExpectedPseudocodeLines()
    {
        var response = _sut.Run([4, 1, 3, 2]);

        response.Steps.Where(s => s.ActionLabel == "pivot_select")
            .Should().OnlyContain(s => s.LineNumber == 11);
        response.Steps.Where(s => s.ActionLabel == "compare")
            .Should().OnlyContain(s => s.LineNumber == 12);
        response.Steps.Where(s => s.ActionLabel == "swap")
            .Should().OnlyContain(s => s.LineNumber == 13);
        response.Steps.Where(s => s.ActionLabel == "pivot_swap")
            .Should().OnlyContain(s => s.LineNumber == 14);
        response.Steps.Where(s => s.ActionLabel == "pivot_placed")
            .Should().OnlyContain(s => s.LineNumber == 15);
    }
}
