using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class MergeSortSimulationEngineTests
{
    private readonly MergeSortSimulationEngine _sut = new();

    // ── CanHandle ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("merge_sort")]
    [InlineData("merge-sort")]
    [InlineData("  MeRgE_SoRt  ")]
    [InlineData("MERGE-SORT")]
    public void CanHandle_RecognizedMergeSortKeys_ReturnsTrue(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bubble_sort")]
    [InlineData("quick_sort")]
    [InlineData("heap_sort")]
    [InlineData("mergesort")]
    [InlineData("merge sort")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    // ── Sorting Correctness ────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-MS-07 - MergeSortEngine: final array state is sorted ascending for unsorted input")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([10, 7, 8, 9, 1, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 5, 7, 8, 9, 10]);
    }

    [Fact(DisplayName = "UT-MS-08 - MergeSortEngine: already-sorted array preserves correct order in final state")]
    public void Run_AlreadySortedArray_FinalStateMatchesInput()
    {
        var response = _sut.Run([1, 2, 3, 4, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact(DisplayName = "UT-MS-09 - MergeSortEngine: reverse-sorted array produces correctly sorted final state")]
    public void Run_ReverseSortedArray_FinalStateIsSortedAscending()
    {
        var response = _sut.Run([5, 4, 3, 2, 1]);

        response.Steps.Last().ArrayState.Should().Equal([1, 2, 3, 4, 5]);
    }

    [Fact(DisplayName = "UT-MS-10 - MergeSortEngine: array with duplicates produces correctly sorted final state")]
    public void Run_ArrayWithDuplicates_FinalStateIsSortedAscending()
    {
        var response = _sut.Run([3, 1, 4, 1, 5, 9, 2, 6, 5]);

        response.Steps.Last().ArrayState.Should().Equal([1, 1, 2, 3, 4, 5, 5, 6, 9]);
    }

    [Fact(DisplayName = "UT-MS-11 - MergeSortEngine: all-identical-values array final state equals input")]
    public void Run_AllIdenticalValues_FinalStateUnchanged()
    {
        var response = _sut.Run([3, 3, 3, 3]);

        response.Steps.Last().ArrayState.Should().Equal([3, 3, 3, 3]);
    }

    [Fact(DisplayName = "UT-MS-12 - MergeSortEngine: two-element unsorted pair is sorted correctly")]
    public void Run_TwoElementUnsortedPair_SortedCorrectly()
    {
        var response = _sut.Run([5, 2]);

        response.Steps.Last().ArrayState.Should().Equal([2, 5]);
    }

    [Fact(DisplayName = "UT-MS-13 - MergeSortEngine: negative numbers are sorted correctly")]
    public void Run_NegativeNumbers_SortedCorrectly()
    {
        var response = _sut.Run([-3, -1, -2]);

        response.Steps.Last().ArrayState.Should().Equal([-3, -2, -1]);
    }

    [Fact(DisplayName = "UT-MS-14 - MergeSortEngine: mixed positive and negative values are sorted correctly")]
    public void Run_MixedPositiveAndNegative_SortedCorrectly()
    {
        var response = _sut.Run([3, -1, 2, -4]);

        response.Steps.Last().ArrayState.Should().Equal([-4, -1, 2, 3]);
    }

    [Fact(DisplayName = "UT-MS-15 - MergeSortEngine: single-element array leaves array state unchanged")]
    public void Run_SingleElementArray_ArrayStateUnchanged()
    {
        var response = _sut.Run([42]);

        response.Steps.Last().ArrayState.Should().Equal([42]);
    }

    // ── Empty / Single-Element Edge Cases ─────────────────────────────────────

    [Fact(DisplayName = "UT-MS-16 - MergeSortEngine: empty array does not throw and emits exactly 3 steps: start, base_case, complete")]
    public void Run_EmptyArray_DoesNotThrowAndEmitsThreeSteps()
    {
        var act = () => _sut.Run([]);
        act.Should().NotThrow();

        var response = _sut.Run([]);

        response.Steps.Should().HaveCount(3);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("base_case");
        response.Steps[2].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-MS-17 - MergeSortEngine: single-element array emits exactly 3 steps: start, base_case, complete")]
    public void Run_SingleElementArray_EmitsExactlyThreeStepsInOrder()
    {
        var response = _sut.Run([7]);

        response.Steps.Should().HaveCount(3);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("base_case");
        response.Steps[2].ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-MS-18 - MergeSortEngine: base_case step for empty array has zero active indices")]
    public void Run_EmptyArray_BaseCaseStepHasZeroActiveIndices()
    {
        var response = _sut.Run([]);

        var baseCaseStep = response.Steps.First(s => s.ActionLabel == "base_case");

        baseCaseStep.ActiveIndices.Should().BeEmpty();
    }

    [Fact(DisplayName = "UT-MS-19 - MergeSortEngine: base_case step for single-element array has exactly one active index pointing to index 0")]
    public void Run_SingleElementArray_BaseCaseStepHasOneActiveIndex()
    {
        var response = _sut.Run([99]);

        var baseCaseStep = response.Steps.First(s => s.ActionLabel == "base_case");

        baseCaseStep.ActiveIndices.Should().ContainSingle().Which.Should().Be(0);
    }

    // ── Step Sequence & Structural Consistency ─────────────────────────────────

    [Fact(DisplayName = "UT-MS-20 - MergeSortEngine: first step is 'start' and last step is 'complete'")]
    public void Run_StepSequence_FirstIsStartAndLastIsComplete()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        response.Steps.First().ActionLabel.Should().Be("start");
        response.Steps.Last().ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-MS-21 - MergeSortEngine: StepNumber starts at 1 and increments by 1 with no gaps")]
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

    [Fact(DisplayName = "UT-MS-22 - MergeSortEngine: TotalSteps matches the actual count of steps in the list")]
    public void Run_TotalSteps_MatchesStepsCount()
    {
        var response = _sut.Run([6, 3, 8, 2, 9]);

        response.TotalSteps.Should().Be(response.Steps.Count);
    }

    [Fact(DisplayName = "UT-MS-23 - MergeSortEngine: AlgorithmName is 'Merge Sort'")]
    public void Run_AlgorithmName_IsMergeSort()
    {
        var response = _sut.Run([1, 2]);

        response.AlgorithmName.Should().Be("Merge Sort");
    }

    [Fact(DisplayName = "UT-MS-24 - MergeSortEngine: ArrayState length matches input length on every step")]
    public void Run_AllSteps_ArrayStateLengthMatchesInput()
    {
        var input = new[] { 5, 3, 8, 1, 2 };
        var response = _sut.Run(input);

        response.Steps.Should().OnlyContain(s => s.ArrayState.Length == input.Length);
    }

    [Fact(DisplayName = "UT-MS-25 - MergeSortEngine: ArrayState is a deep-copy snapshot — start shows original, complete shows sorted")]
    public void Run_ArrayState_IsSnapshotedIndependentlyPerStep()
    {
        var response = _sut.Run([3, 1, 2]);

        var startState    = response.Steps.First(s => s.ActionLabel == "start").ArrayState;
        var completeState = response.Steps.Last(s  => s.ActionLabel == "complete").ArrayState;

        startState.Should().Equal([3, 1, 2]);
        completeState.Should().Equal([1, 2, 3]);
    }

    [Fact(DisplayName = "UT-MS-26 - MergeSortEngine: 'start' and 'complete' each appear exactly once")]
    public void Run_StartAndComplete_EachAppearExactlyOnce()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        response.Steps.Count(s => s.ActionLabel == "start").Should().Be(1);
        response.Steps.Count(s => s.ActionLabel == "complete").Should().Be(1);
    }

    [Fact(DisplayName = "UT-MS-27 - MergeSortEngine: all ActionLabel values are snake_case with no uppercase letters")]
    public void Run_AllActionLabels_AreSnakeCase()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s =>
            s.ActionLabel == s.ActionLabel.ToLowerInvariant(),
            because: "ActionLabel must follow snake_case — no uppercase characters are permitted");
    }

    [Fact(DisplayName = "UT-MS-28 - MergeSortEngine: multi-element array contains all mandatory action labels")]
    public void Run_MultiElementArray_ContainsAllMandatoryActionLabels()
    {
        var response = _sut.Run([5, 3, 8, 1, 6]);

        var labels = response.Steps.Select(s => s.ActionLabel).ToHashSet();

        labels.Should().Contain("start");
        labels.Should().Contain("split");
        labels.Should().Contain("compare");
        labels.Should().Contain("place");
        labels.Should().Contain("complete");
    }

    [Fact(DisplayName = "UT-MS-29 - MergeSortEngine: two-element array produces exactly 6 steps in order: start, split, compare, place, place, complete")]
    public void Run_TwoElementArray_ProducesExactlySixStepsInOrder()
    {
        var response = _sut.Run([5, 2]);

        response.Steps.Should().HaveCount(6);
        response.Steps[0].ActionLabel.Should().Be("start");
        response.Steps[1].ActionLabel.Should().Be("split");
        response.Steps[2].ActionLabel.Should().Be("compare");
        response.Steps[3].ActionLabel.Should().Be("place");
        response.Steps[4].ActionLabel.Should().Be("place");
        response.Steps[5].ActionLabel.Should().Be("complete");
    }

    // ── MergeSort Metadata ─────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-MS-30 - MergeSortEngine: every step has a non-null MergeSort metadata model")]
    public void Run_AllSteps_HaveNonNullMergeSortModel()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s => s.MergeSort != null);
    }

    [Fact(DisplayName = "UT-MS-31 - MergeSortEngine: all MergeSort.Type values are snake_case with no uppercase letters")]
    public void Run_AllMergeSortTypes_AreSnakeCase()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        response.Steps.Should().OnlyContain(s =>
            s.MergeSort != null &&
            s.MergeSort.Type == s.MergeSort.Type.ToLowerInvariant(),
            because: "MergeSort.Type must follow snake_case convention");
    }

    [Fact(DisplayName = "UT-MS-32 - MergeSortEngine: split steps have non-null Mid satisfying Left ≤ Mid < Right")]
    public void Run_SplitSteps_HaveMidWithinBounds()
    {
        var response = _sut.Run([4, 2, 7, 1, 5]);

        var splitSteps = response.Steps.Where(s => s.ActionLabel == "split").ToArray();

        splitSteps.Should().NotBeEmpty();
        splitSteps.Should().OnlyContain(s =>
            s.MergeSort != null &&
            s.MergeSort.Mid.HasValue &&
            s.MergeSort.Mid.Value >= s.MergeSort.Left &&
            s.MergeSort.Mid.Value < s.MergeSort.Right,
            because: "every split step must record a midpoint within its subrange");
    }

    [Fact(DisplayName = "UT-MS-33 - MergeSortEngine: compare and place steps retain a valid Mid for the active merge range")]
    public void Run_MergePassSteps_RetainValidMid()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var mergePassSteps = response.Steps
            .Where(s => s.ActionLabel is "compare" or "place")
            .ToArray();

        mergePassSteps.Should().NotBeEmpty();
        mergePassSteps.Should().OnlyContain(s =>
            s.MergeSort != null
            && s.MergeSort.Mid.HasValue
            && s.MergeSort.Mid.Value >= s.MergeSort.Left
            && s.MergeSort.Mid.Value < s.MergeSort.Right,
            because: "merge-pass steps need the midpoint to describe which two sorted halves are being merged");
    }

    [Fact(DisplayName = "UT-MS-34 - MergeSortEngine: compare steps have non-null MergeBuffer")]
    public void Run_CompareSteps_HaveNonNullMergeBuffer()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var compareSteps = response.Steps.Where(s => s.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(s =>
            s.MergeSort != null && s.MergeSort.MergeBuffer != null);
    }

    [Fact(DisplayName = "UT-MS-35 - MergeSortEngine: place steps have non-null PlaceIndex within array bounds")]
    public void Run_PlaceSteps_HaveNonNullPlaceIndexWithinBounds()
    {
        var input = new[] { 4, 2, 7, 1 };
        var response = _sut.Run(input);

        var placeSteps = response.Steps.Where(s => s.ActionLabel == "place").ToArray();

        placeSteps.Should().NotBeEmpty();
        placeSteps.Should().OnlyContain(s =>
            s.MergeSort != null &&
            s.MergeSort.PlaceIndex.HasValue &&
            s.MergeSort.PlaceIndex.Value >= 0 &&
            s.MergeSort.PlaceIndex.Value < input.Length);
    }

    [Fact(DisplayName = "UT-MS-36 - MergeSortEngine: compare steps do not have PlaceIndex set")]
    public void Run_CompareSteps_HaveNullPlaceIndex()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var compareSteps = response.Steps.Where(s => s.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(s =>
            s.MergeSort != null && !s.MergeSort.PlaceIndex.HasValue,
            because: "PlaceIndex is only populated on place steps, not compare steps");
    }

    [Fact(DisplayName = "UT-MS-37 - MergeSortEngine: RecursionDepth is non-negative on every step")]
    public void Run_AllSteps_RecursionDepthIsNonNegative()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s =>
            s.MergeSort != null && s.MergeSort.RecursionDepth >= 0);
    }

    [Fact(DisplayName = "UT-MS-38 - MergeSortEngine: at least one step has RecursionDepth > 0 for a multi-element array")]
    public void Run_MultiElementArray_RecursionDepthExceedsZeroInNestedCalls()
    {
        var response = _sut.Run([4, 2, 6, 1, 5]);

        var maxDepth = response.Steps
            .Where(s => s.MergeSort != null)
            .Max(s => s.MergeSort!.RecursionDepth);

        maxDepth.Should().BeGreaterThan(0,
            because: "sorting a multi-element array must recurse into at least one sub-partition");
    }

    [Fact(DisplayName = "UT-MS-39 - MergeSortEngine: Left and Right are valid bounds on every non-empty-array step")]
    public void Run_AllSteps_LeftAndRightAreWithinArrayBounds()
    {
        var input = new[] { 5, 3, 8, 1, 2 };
        var response = _sut.Run(input);

        response.Steps.Should().OnlyContain(s =>
            s.MergeSort != null &&
            s.MergeSort.Left >= 0 &&
            s.MergeSort.Right < input.Length &&
            s.MergeSort.Left <= s.MergeSort.Right);
    }

    [Fact(DisplayName = "UT-MS-40 - MergeSortEngine: MergeBuffer for the first compare step in a merge pass equals the concatenated sorted halves")]
    public void Run_FirstCompareStepInMergePass_MergeBufferEqualsFullHalves()
    {
        // [5, 2]: leftPart=[5], rightPart=[2], so first compare's buffer must be [5, 2]
        var response = _sut.Run([5, 2]);

        var firstCompare = response.Steps.First(s => s.ActionLabel == "compare");

        firstCompare.MergeSort!.MergeBuffer.Should().Equal([5, 2],
            because: "the merge buffer at the first compare captures both sorted halves before any placement");
    }

    [Fact(DisplayName = "UT-MS-41 - MergeSortEngine: MergeBuffer shrinks by exactly 1 after each place step within a single merge pass")]
    public void Run_MergeBuffer_ShrinksExactlyOnePerPlaceStep()
    {
        // [5, 2]: single merge pass — easy to trace the buffer length progression
        var response = _sut.Run([5, 2]);

        var mergePassSteps = response.Steps
            .Where(s => s.ActionLabel is "compare" or "place")
            .ToArray();

        for (var i = 1; i < mergePassSteps.Length; i++)
        {
            var prev = mergePassSteps[i - 1].MergeSort!.MergeBuffer!.Length;
            var curr = mergePassSteps[i].MergeSort!.MergeBuffer!.Length;

            curr.Should().Be(prev - 1,
                because: $"each step in a merge pass should remove one element from the buffer (step {i})");
        }
    }

    // ── Active Indices ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-MS-44 - MergeSortEngine: all ActiveIndices are within array bounds on every step")]
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

    [Fact(DisplayName = "UT-MS-45 - MergeSortEngine: compare steps have exactly two active indices")]
    public void Run_CompareSteps_HaveExactlyTwoActiveIndices()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var compareSteps = response.Steps.Where(s => s.ActionLabel == "compare").ToArray();

        compareSteps.Should().NotBeEmpty();
        compareSteps.Should().OnlyContain(s => s.ActiveIndices.Length == 2,
            because: "each compare step marks both candidates being compared in the merge pass");
    }

    [Fact(DisplayName = "UT-MS-46 - MergeSortEngine: place steps have exactly one active index")]
    public void Run_PlaceSteps_HaveExactlyOneActiveIndex()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var placeSteps = response.Steps.Where(s => s.ActionLabel == "place").ToArray();

        placeSteps.Should().NotBeEmpty();
        placeSteps.Should().OnlyContain(s => s.ActiveIndices.Length == 1,
            because: "each place step marks only the single destination index in the main array");
    }

    [Fact(DisplayName = "UT-MS-47 - MergeSortEngine: split step active indices span the full subrange from Left to Right")]
    public void Run_SplitSteps_ActiveIndicesSpanFullSubrange()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var splitSteps = response.Steps.Where(s => s.ActionLabel == "split").ToArray();

        splitSteps.Should().NotBeEmpty();
        splitSteps.Should().OnlyContain(s =>
            s.MergeSort != null
            && s.ActiveIndices.SequenceEqual(
                Enumerable.Range(s.MergeSort.Left, s.MergeSort.Right - s.MergeSort.Left + 1)),
            because: "a split step must highlight the entire subrange that is being divided");
    }

    // ── Recursion Model ────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-MS-48 - MergeSortEngine: every step has a non-null Recursion metadata model")]
    public void Run_AllSteps_HaveNonNullRecursionModel()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        response.Steps.Should().OnlyContain(s => s.Recursion != null);
    }

    [Fact(DisplayName = "UT-MS-49 - MergeSortEngine: 'start' step has an empty recursion stack and no current frame")]
    public void Run_StartStep_HasEmptyRecursionStackAndNoCurrentFrame()
    {
        var response = _sut.Run([4, 2, 7]);

        var startStep = response.Steps.First(s => s.ActionLabel == "start");

        startStep.Recursion.Should().NotBeNull();
        startStep.Recursion!.Stack.Should().BeEmpty();
        startStep.Recursion.CurrentFrameId.Should().BeNull();
    }

    [Fact(DisplayName = "UT-MS-50 - MergeSortEngine: 'complete' step has an empty recursion stack and no current frame")]
    public void Run_CompleteStep_HasEmptyRecursionStackAndNoCurrentFrame()
    {
        var response = _sut.Run([4, 2, 7]);

        var completeStep = response.Steps.Last(s => s.ActionLabel == "complete");

        completeStep.Recursion.Should().NotBeNull();
        completeStep.Recursion!.Stack.Should().BeEmpty();
        completeStep.Recursion.CurrentFrameId.Should().BeNull();
    }

    [Fact(DisplayName = "UT-MS-51 - MergeSortEngine: multi-element array produces a max recursion stack depth greater than 1")]
    public void Run_MultiElementArray_StackDepthExceedsOneInNestedCalls()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        var maxStackSize = response.Steps
            .Where(s => s.Recursion?.Stack.Count > 0)
            .Max(s => s.Recursion!.Stack.Count);

        maxStackSize.Should().BeGreaterThan(1,
            because: "sorting four elements requires at least two levels of recursion");
    }

    [Fact(DisplayName = "UT-MS-52 - MergeSortEngine: recursion frame IDs are unique integers starting from 1")]
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

    // ── Split Count Invariant ──────────────────────────────────────────────────

    [Theory(DisplayName = "UT-MS-53 - MergeSortEngine: for an array of size n > 1, exactly n-1 split steps are emitted")]
    [InlineData(new[] { 5, 2 },             1)]
    [InlineData(new[] { 3, 1, 2 },          2)]
    [InlineData(new[] { 4, 3, 2, 1 },       3)]
    [InlineData(new[] { 5, 4, 3, 2, 1 },    4)]
    [InlineData(new[] { 6, 5, 4, 3, 2, 1 }, 5)]
    public void Run_MultiElementArray_SplitCountIsNMinusOne(int[] input, int expectedSplits)
    {
        var response = _sut.Run(input);

        response.Steps.Count(s => s.ActionLabel == "split").Should().Be(expectedSplits,
            because: $"a {input.Length}-element array has exactly {expectedSplits} internal nodes in its recursion tree");
    }

    // ── Line Numbers ───────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-MS-54 - MergeSortEngine: every step maps to a positive pseudocode line number")]
    public void Run_AllSteps_HavePositiveLineNumber()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        response.Steps.Should().OnlyContain(s => s.LineNumber > 0,
            because: "every step must map to a pseudocode line for visualization");
    }

    [Fact(DisplayName = "UT-MS-55 - MergeSortEngine: each emitted step type maps to the correct pseudocode line number")]
    public void Run_StepTypes_MapToCorrectLineNumbers()
    {
        var response = _sut.Run([3, 1, 2]);

        var byLabel = response.Steps.GroupBy(s => s.ActionLabel)
            .ToDictionary(g => g.Key, g => g.First().LineNumber);

        byLabel["start"].Should().Be(1,     because: "start   → Line 1");
        byLabel["split"].Should().Be(4,     because: "split   → Line 4");
        byLabel["compare"].Should().Be(8,   because: "compare → Line 8");
        byLabel["place"].Should().Be(9,     because: "place   → Line 9");
        byLabel["complete"].Should().Be(12, because: "complete → Line 12");
    }
}
