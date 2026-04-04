using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class HeapSortSimulationEngineTests
{
    private readonly HeapSortSimulationEngine _sut = new();

    // ─────────────────────────────────────────────────────────────────────────
    // HEAPIFY CORRECTNESS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-HEAP-01 - HeapSortEngine: after heapify phase the root must be the largest element")]
    public void Run_AfterHeapifyPhase_RootIsLargestElement()
    {
        var response = _sut.Run([3, 1, 4, 1, 5, 9, 2, 6]);

        var heapifyComplete = response.Steps
            .First(s => s.ActionLabel == "heapify_phase_complete");

        var arrayAtCompletion = heapifyComplete.ArrayState;
        arrayAtCompletion[0].Should().Be(arrayAtCompletion.Max(),
            because: "the root must hold the maximum element once the max-heap is built");
    }

    [Fact(DisplayName = "UT-HEAP-02 - HeapSortEngine: heapify phase emits at least one heapify_compare step for multi-element input")]
    public void Run_HeapifyPhase_EmitsCompareSteps_ForMultiElementArray()
    {
        var response = _sut.Run([5, 3, 8, 1]);

        var compareSteps = response.Steps
            .Where(s => s.ActionLabel == "heapify_compare")
            .ToArray();

        compareSteps.Should().NotBeEmpty(
            because: "sifting down during build-heap always produces at least one comparison");
    }

    [Fact(DisplayName = "UT-HEAP-03 - HeapSortEngine: heapify_compare steps carry two active indices")]
    public void Run_HeapifyCompareSteps_HaveTwoActiveIndices()
    {
        var response = _sut.Run([4, 10, 3, 5, 1]);

        var compareSteps = response.Steps
            .Where(s => s.ActionLabel == "heapify_compare")
            .ToArray();

        foreach (var step in compareSteps)
        {
            step.ActiveIndices.Should().HaveCount(2,
                because: "each comparison involves exactly a parent and a child index");
        }
    }

    [Fact(DisplayName = "UT-HEAP-04 - HeapSortEngine: every swap step during heapify has its two indices in active indices")]
    public void Run_SwapStepsDuringHeapify_ContainBothSwappedIndices()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        var extractionStart = response.Steps
            .FirstOrDefault(s => s.ActionLabel == "extraction_phase_start");

        var heapifySwaps = response.Steps
            .TakeWhile(s => s != extractionStart)
            .Where(s => s.ActionLabel == "swap")
            .ToArray();

        foreach (var step in heapifySwaps)
        {
            step.ActiveIndices.Should().HaveCount(2,
                because: "a swap always involves exactly two distinct positions");
            step.ActiveIndices[0].Should().NotBe(step.ActiveIndices[1],
                because: "a swap between the same index is meaningless");
        }
    }

    [Fact(DisplayName = "UT-HEAP-05 - HeapSortEngine: single-element array completes heapify with no swap steps")]
    public void Run_SingleElementArray_HeapifyProducesNoSwaps()
    {
        var response = _sut.Run([42]);

        var swapSteps = response.Steps
            .Where(s => s.ActionLabel == "swap")
            .ToArray();

        swapSteps.Should().BeEmpty(
            because: "a single-element array is already a valid heap — no swaps needed");
    }

    [Fact(DisplayName = "UT-HEAP-06 - HeapSortEngine: already-max-heap input produces no swap steps during heapify")]
    public void Run_AlreadyMaxHeapInput_ProducesNoSwapsDuringHeapify()
    {
        // [9, 5, 7, 2, 3, 1] is a valid max-heap
        var response = _sut.Run([9, 5, 7, 2, 3, 1]);

        var extractionStart = response.Steps
            .FirstOrDefault(s => s.ActionLabel == "extraction_phase_start");

        var heapifySwaps = response.Steps
            .TakeWhile(s => s != extractionStart)
            .Where(s => s.ActionLabel == "swap")
            .ToArray();

        heapifySwaps.Should().BeEmpty(
            because: "an already-valid max-heap requires no swaps to build");
    }

    [Fact(DisplayName = "UT-HEAP-07 - HeapSortEngine: each step's ArrayState is an independent snapshot")]
    public void Run_ArrayStateInEachStep_IsAnImmutableSnapshot()
    {
        var response = _sut.Run([7, 2, 9, 4]);

        for (var i = 0; i < response.Steps.Count - 1; i++)
        {
            response.Steps[i].ArrayState.Should()
                .NotBeSameAs(response.Steps[i + 1].ArrayState,
                    because: "each step must capture its own independent snapshot of the array");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EXTRACTION SEQUENCE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-HEAP-08 - HeapSortEngine: extraction phase produces exactly (n-1) root-swap steps for n-element array")]
    public void Run_ExtractionPhase_ProducesExactlyNMinusOneRootSwaps()
    {
        var input = new[] { 5, 3, 8, 1, 9 };
        var response = _sut.Run(input);

        var rootSwapSteps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "root_end_swap")
            .ToArray();

        rootSwapSteps.Should().HaveCount(input.Length - 1,
            because: "heap sort extracts the max n-1 times to produce a sorted array");
    }

    [Fact(DisplayName = "UT-HEAP-09 - HeapSortEngine: final array state is sorted in ascending order")]
    public void Run_FinalStep_ArrayStateIsSortedAscending()
    {
        var response = _sut.Run([5, 3, 8, 1, 9, 2]);

        var finalArrayState = response.Steps.Last().ArrayState;

        finalArrayState.Should().BeInAscendingOrder(
            because: "heap sort must produce a fully sorted array");
    }

    [Fact(DisplayName = "UT-HEAP-10 - HeapSortEngine: each root-swap step has index 0 as one of the active indices")]
    public void Run_RootSwapSteps_AlwaysInvolveIndexZero()
    {
        var response = _sut.Run([4, 2, 7, 1, 8]);

        var rootSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "root_end_swap")
            .ToArray();

        foreach (var step in rootSwaps)
        {
            step.ActiveIndices.Should().Contain(0,
                because: "each extraction swaps the heap root (index 0) with the last heap element");
        }
    }

    [Fact(DisplayName = "UT-HEAP-11 - HeapSortEngine: heap boundary shrinks by 1 after each root extraction")]
    public void Run_ExtractionSteps_HeapBoundaryDecreasesByOneEachRound()
    {
        var response = _sut.Run([3, 1, 4, 1, 5, 9]);

        var rootSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "root_end_swap")
            .OrderBy(s => s.StepNumber)
            .ToArray();

        for (var i = 1; i < rootSwaps.Length; i++)
        {
            rootSwaps[i].Heap!.HeapBoundaryEnd.Should().BeLessThan(
                rootSwaps[i - 1].Heap!.HeapBoundaryEnd,
                because: "every extraction reduces the heap region by one position");
        }
    }

    [Fact(DisplayName = "UT-HEAP-12 - HeapSortEngine: extracted elements appear at the tail in sorted order")]
    public void Run_ExtractedElements_AppearAtTailInDescendingOrder()
    {
        var input = new[] { 5, 3, 8, 1, 9 };
        var response = _sut.Run(input);
        var n = input.Length;

        var rootSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "root_end_swap")
            .OrderBy(s => s.StepNumber)
            .ToArray();

        for (var i = 0; i < rootSwaps.Length; i++)
        {
            var positionJustPlaced = n - 1 - i;
            var valueAtTail = rootSwaps[i].ArrayState[positionJustPlaced];

            for (var j = positionJustPlaced + 1; j < n; j++)
            {
                rootSwaps[i].ArrayState[j].Should().BeGreaterThanOrEqualTo(valueAtTail,
                    because: $"extracted elements are placed in sorted order at the tail; " +
                              $"position {positionJustPlaced} should be <= position {j}");
            }
        }
    }

    [Fact(DisplayName = "UT-HEAP-13 - HeapSortEngine: extraction phase emits sift-down compare steps after each extraction")]
    public void Run_ExtractionPhase_EmitsSiftDownCompareSteps()
    {
        var response = _sut.Run([4, 10, 3, 5, 1]);

        var extractSiftCompares = response.Steps
            .Where(s => s.ActionLabel == "extract_heapify_compare")
            .ToArray();

        extractSiftCompares.Should().NotBeEmpty(
            because: "after each root extraction the new root must be sifted down");
    }

    [Fact(DisplayName = "UT-HEAP-14 - HeapSortEngine: last step action label is 'complete'")]
    public void Run_LastStep_ActionLabelIsComplete()
    {
        var response = _sut.Run([7, 3, 5]);

        response.Steps.Last().ActionLabel.Should().Be("complete",
            because: "the simulation must terminate with a 'complete' sentinel step");
    }

    [Fact(DisplayName = "UT-HEAP-15 - HeapSortEngine: TotalSteps matches actual steps count")]
    public void Run_TotalSteps_MatchesActualStepCount()
    {
        var response = _sut.Run([2, 8, 4, 6]);

        response.TotalSteps.Should().Be(response.Steps.Count,
            because: "TotalSteps must accurately reflect the number of generated steps");
    }

    [Fact(DisplayName = "UT-HEAP-16 - HeapSortEngine: step numbers are sequential starting from 1")]
    public void Run_StepNumbers_AreSequentialFromOne()
    {
        var response = _sut.Run([3, 1, 4]);

        for (var i = 0; i < response.Steps.Count; i++)
        {
            response.Steps[i].StepNumber.Should().Be(i + 1,
                because: "step numbers must be sequential starting at 1");
        }
    }

    [Fact(DisplayName = "UT-HEAP-17 - HeapSortEngine: empty array does not throw and returns a complete step")]
    public void Run_EmptyArray_DoesNotThrow_AndReturnsCompleteStep()
    {
        var act = () => _sut.Run([]);
        act.Should().NotThrow();

        var response = _sut.Run([]);
        response.Steps.Last().ActionLabel.Should().Be("complete");
    }

    [Fact(DisplayName = "UT-HEAP-18 - HeapSortEngine: reverse-sorted array produces correctly ascending sorted result")]
    public void Run_ReverseSortedArray_ProducesCorrectlyAscendingSortedResult()
    {
        var response = _sut.Run([9, 8, 7, 6, 5, 4, 3, 2, 1]);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "a reverse-sorted input is the worst case and must still sort correctly");
    }

    [Fact(DisplayName = "UT-HEAP-19 - HeapSortEngine: duplicate elements are handled correctly — result is sorted ascending")]
    public void Run_ArrayWithDuplicates_ProducesSortedAscendingResult()
    {
        var response = _sut.Run([4, 4, 2, 2, 8, 8]);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "duplicate values must not break the sort correctness");
    }

    [Fact(DisplayName = "UT-HEAP-20 - HeapSortEngine: all-equal elements produce no sift-down swap steps")]
    public void Run_AllEqualElements_ProduceNoSiftDownSwapSteps()
    {
        // The extraction loop unconditionally swaps root ↔ tail (n-1 root_end_swap steps),
        // but sift-down must never find a child larger than the new root when all values
        // are identical — so there should be zero sift-down swap steps.
        var response = _sut.Run([5, 5, 5, 5]);

        var siftDownSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison != "root_end_swap")
            .ToArray();

        siftDownSwaps.Should().BeEmpty(
            because: "when all elements are identical the new root after each extraction " +
                     "is already the heap maximum — no sift-down swap is needed");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CAN HANDLE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-HEAP-21 - HeapSortEngine: CanHandle returns true for 'heap_sort' (underscore variant)")]
    public void CanHandle_ReturnsTrueForHeapSort_UnderscoreVariant()
    {
        _sut.CanHandle("heap_sort").Should().BeTrue(
            because: "heap_sort is the primary normalized key for this engine");
    }

    [Fact(DisplayName = "UT-HEAP-22 - HeapSortEngine: CanHandle returns true for 'heap-sort' (hyphen variant)")]
    public void CanHandle_ReturnsTrueForHeapSort_HyphenVariant()
    {
        _sut.CanHandle("heap-sort").Should().BeTrue(
            because: "heap-sort is the hyphenated alias accepted by this engine");
    }

    [Theory(DisplayName = "UT-HEAP-23 - HeapSortEngine: CanHandle is case-insensitive")]
    [InlineData("HEAP_SORT")]
    [InlineData("Heap-Sort")]
    [InlineData("HEAP-SORT")]
    [InlineData("Heap_Sort")]
    public void CanHandle_IsCaseInsensitive(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeTrue(
            because: $"CanHandle must accept '{algorithm}' regardless of casing");
    }

    [Theory(DisplayName = "UT-HEAP-24 - HeapSortEngine: CanHandle returns false for other algorithm names")]
    [InlineData("bubble_sort")]
    [InlineData("quick_sort")]
    [InlineData("binary_search")]
    [InlineData("merge_sort")]
    [InlineData("insertion_sort")]
    public void CanHandle_ReturnsFalseForOtherAlgorithms(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse(
            because: $"'{algorithm}' is not handled by the HeapSort engine");
    }

    [Theory(DisplayName = "UT-HEAP-25 - HeapSortEngine: CanHandle returns false for empty string or unseparated name")]
    [InlineData("")]
    [InlineData("heapsort")]
    [InlineData("heap sort")]
    [InlineData("   ")]
    public void CanHandle_ReturnsFalseForEmptyOrGarbage(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse(
            because: $"'{algorithm}' does not match any supported heap-sort key");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // INPUT DATA EDGE CASES
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-HEAP-26 - HeapSortEngine: two-element array sorts correctly to ascending order")]
    public void Run_TwoElementArray_SortsCorrectly()
    {
        var response = _sut.Run([5, 2]);

        response.Steps.Last().ArrayState.Should().Equal(new[] { 2, 5 },
            "two-element input must be sorted ascending");
    }

    [Fact(DisplayName = "UT-HEAP-27 - HeapSortEngine: two-element max-heap input produces no heapify swap steps")]
    public void Run_TwoElementMaxHeap_ProducesNoHeapifySwaps()
    {
        // [5, 2]: parent(0)=5 > child(1)=2 → already a valid max-heap
        var response = _sut.Run([5, 2]);

        var extractionStart = response.Steps
            .FirstOrDefault(s => s.ActionLabel == "extraction_phase_start");

        var heapifySwaps = response.Steps
            .TakeWhile(s => s != extractionStart)
            .Where(s => s.ActionLabel == "swap")
            .ToArray();

        heapifySwaps.Should().BeEmpty(
            because: "[5, 2] is already a valid max-heap — no heapify swap should be needed");
    }

    [Fact(DisplayName = "UT-HEAP-28 - HeapSortEngine: already-sorted ascending array still produces a correct sorted result")]
    public void Run_AlreadySortedAscending_ProducesSortedResult()
    {
        var response = _sut.Run([1, 2, 3, 4, 5]);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "already-sorted ascending input is not a max-heap and requires heapify work, " +
                     "but the final output must still be sorted ascending");
    }

    [Fact(DisplayName = "UT-HEAP-29 - HeapSortEngine: array of negative numbers is sorted in ascending order")]
    public void Run_NegativeNumbers_SortsCorrectly()
    {
        var response = _sut.Run([-3, -1, -4, -2]);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "negative values must be sorted ascending just like positive values");
        response.Steps.Last().ArrayState.Should().Equal(-4, -3, -2, -1);
    }

    [Fact(DisplayName = "UT-HEAP-30 - HeapSortEngine: mixed negative and positive values are sorted in ascending order")]
    public void Run_MixedNegativeAndPositive_SortsCorrectly()
    {
        var response = _sut.Run([-2, 5, -1, 3]);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "a mix of negative and positive values must be sorted ascending");
    }

    [Fact(DisplayName = "UT-HEAP-31 - HeapSortEngine: array spanning int.MinValue to int.MaxValue is sorted correctly")]
    public void Run_ArrayWithMinAndMaxInt_SortsCorrectly()
    {
        var response = _sut.Run([int.MaxValue, 0, int.MinValue]);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "boundary integer values must not cause overflow during comparisons");
        response.Steps.Last().ArrayState.Should().Equal(int.MinValue, 0, int.MaxValue);
    }

    [Fact(DisplayName = "UT-HEAP-32 - HeapSortEngine: large array (100 elements, reverse-ordered) is sorted correctly")]
    public void Run_LargeArray_SortsCorrectly()
    {
        var input = Enumerable.Range(1, 100).Reverse().ToArray(); // [100, 99, ..., 1]
        var response = _sut.Run(input);

        response.Steps.Last().ArrayState.Should().BeInAscendingOrder(
            because: "a large reverse-ordered array must still be correctly sorted");
    }

    [Fact(DisplayName = "UT-HEAP-33 - HeapSortEngine: all-same negative elements produce no sift-down swap steps")]
    public void Run_AllSameNegativeElements_ProduceNoSiftDownSwapSteps()
    {
        var response = _sut.Run([-5, -5, -5, -5]);

        var siftDownSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison != "root_end_swap")
            .ToArray();

        siftDownSwaps.Should().BeEmpty(
            because: "when all elements are equal (even negative) sift-down never finds " +
                     "a child strictly greater than the parent");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HEAP METADATA CORRECTNESS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-HEAP-34 - HeapSortEngine: heapify_compare steps have Phase set to 'heapify'")]
    public void Run_HeapifyCompareSteps_HavePhase_Heapify()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        var heapifyCompares = response.Steps
            .Where(s => s.ActionLabel == "heapify_compare")
            .ToArray();

        heapifyCompares.Should().NotBeEmpty();
        foreach (var step in heapifyCompares)
        {
            step.Heap.Should().NotBeNull();
            step.Heap!.Phase.Should().Be("heapify",
                because: "all heapify_compare steps belong to the heapify phase");
        }
    }

    [Fact(DisplayName = "UT-HEAP-35 - HeapSortEngine: extract_heapify_compare steps have Phase set to 'extraction'")]
    public void Run_ExtractionCompareSteps_HavePhase_Extraction()
    {
        var response = _sut.Run([4, 10, 3, 5, 1]);

        var extractionCompares = response.Steps
            .Where(s => s.ActionLabel == "extract_heapify_compare")
            .ToArray();

        extractionCompares.Should().NotBeEmpty();
        foreach (var step in extractionCompares)
        {
            step.Heap.Should().NotBeNull();
            step.Heap!.Phase.Should().Be("extraction",
                because: "all extract_heapify_compare steps belong to the extraction phase");
        }
    }

    [Fact(DisplayName = "UT-HEAP-36 - HeapSortEngine: every step has non-null Heap metadata")]
    public void Run_AllSteps_HaveNonNullHeapMetadata()
    {
        var response = _sut.Run([5, 3, 8, 1, 9]);

        foreach (var step in response.Steps)
        {
            step.Heap.Should().NotBeNull(
                because: $"step {step.StepNumber} ('{step.ActionLabel}') must carry Heap metadata");
        }
    }

    [Fact(DisplayName = "UT-HEAP-37 - HeapSortEngine: root_end_swap steps have correct SortedTargetIndex")]
    public void Run_RootEndSwapSteps_HaveCorrectSortedTargetIndex()
    {
        var input = new[] { 5, 3, 8, 1, 9 };
        var n = input.Length;
        var response = _sut.Run(input);

        var rootSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "root_end_swap")
            .OrderBy(s => s.StepNumber)
            .ToArray();

        for (var i = 0; i < rootSwaps.Length; i++)
        {
            rootSwaps[i].Heap!.SortedTargetIndex.Should().Be(n - 1 - i,
                because: $"the {i + 1}st extraction places the max at tail position {n - 1 - i}");
        }
    }

    [Fact(DisplayName = "UT-HEAP-38 - HeapSortEngine: root_end_swap steps have ExtractedValue matching the value placed at the sorted tail")]
    public void Run_RootEndSwapSteps_HaveCorrectExtractedValue()
    {
        var response = _sut.Run([5, 3, 8, 1, 9]);

        var rootSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "root_end_swap")
            .ToArray();

        foreach (var step in rootSwaps)
        {
            var targetIndex = step.Heap!.SortedTargetIndex!.Value;
            step.ArrayState[targetIndex].Should().Be(step.Heap.ExtractedValue!.Value,
                because: "ExtractedValue must equal the element placed at the sorted tail position");
        }
    }

    [Fact(DisplayName = "UT-HEAP-39 - HeapSortEngine: heapify_compare steps use only 'parent_left_compare' or 'candidate_right_compare' as comparison type")]
    public void Run_HeapifyCompareSteps_UseValidParentChildComparisons()
    {
        var response = _sut.Run([3, 1, 4, 1, 5, 9, 2, 6]);

        var heapifyCompares = response.Steps
            .Where(s => s.ActionLabel == "heapify_compare")
            .ToArray();

        heapifyCompares.Should().NotBeEmpty();
        foreach (var step in heapifyCompares)
        {
            step.Heap!.ParentChildComparison.Should().BeOneOf(
                new[] { "parent_left_compare", "candidate_right_compare" },
                "heapify compares are either parent-vs-left-child or candidate-vs-right-child comparisons");
        }
    }

    [Fact(DisplayName = "UT-HEAP-40 - HeapSortEngine: candidate_right_compare steps only appear when the right child is within heap bounds")]
    public void Run_CandidateRightCompareSteps_OnlyWhenRightChildInBounds()
    {
        var response = _sut.Run([3, 1, 4, 1, 5, 9, 2, 6]);

        var rightCompares = response.Steps
            .Where(s => (s.ActionLabel == "heapify_compare" || s.ActionLabel == "extract_heapify_compare") &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison == "candidate_right_compare")
            .ToArray();

        foreach (var step in rightCompares)
        {
            step.Heap!.RightChildIndex.Should().HaveValue(
                because: "a right-child compare step must have RightChildIndex populated");
            step.Heap.RightChildIndex!.Value.Should().BeLessThanOrEqualTo(step.Heap.HeapBoundaryEnd,
                because: "the right child must lie within the current heap boundary");
        }
    }

    [Fact(DisplayName = "UT-HEAP-41 - HeapSortEngine: sift-down swap steps during heapify have ParentChildComparison set to 'swap_parent_child'")]
    public void Run_SiftDownSwapsDuringHeapify_HaveComparison_SwapParentChild()
    {
        var response = _sut.Run([1, 2, 3, 4, 5]); // ascending input is not a max-heap; forces heapify swaps

        var extractionStart = response.Steps
            .FirstOrDefault(s => s.ActionLabel == "extraction_phase_start");

        var heapifySwaps = response.Steps
            .TakeWhile(s => s != extractionStart)
            .Where(s => s.ActionLabel == "swap")
            .ToArray();

        heapifySwaps.Should().NotBeEmpty();
        foreach (var step in heapifySwaps)
        {
            step.Heap!.ParentChildComparison.Should().Be("swap_parent_child",
                because: "sift-down swaps during heapify always swap the parent with its largest child");
        }
    }

    [Fact(DisplayName = "UT-HEAP-42 - HeapSortEngine: sift-down swap steps during extraction have ParentChildComparison set to 'swap_parent_child'")]
    public void Run_SiftDownSwapsDuringExtraction_HaveComparison_SwapParentChild()
    {
        var response = _sut.Run([4, 10, 3, 5, 1]);

        var extractionSiftSwaps = response.Steps
            .Where(s => s.ActionLabel == "swap" &&
                        s.Heap != null &&
                        s.Heap.ParentChildComparison != "root_end_swap")
            .ToArray();

        foreach (var step in extractionSiftSwaps)
        {
            step.Heap!.ParentChildComparison.Should().Be("swap_parent_child",
                because: "non-root-end extraction swaps always swap the parent with its largest child");
        }
    }

    [Fact(DisplayName = "UT-HEAP-43 - HeapSortEngine: all heapify-phase compare and swap steps have HeapBoundaryEnd equal to n-1")]
    public void Run_HeapBoundaryEnd_InHeapifyPhase_IsAlwaysNMinusOne()
    {
        var input = new[] { 3, 1, 4, 1, 5 };
        var n = input.Length;
        var response = _sut.Run(input);

        var extractionStart = response.Steps
            .FirstOrDefault(s => s.ActionLabel == "extraction_phase_start");

        var heapifyActiveSteps = response.Steps
            .TakeWhile(s => s != extractionStart)
            .Where(s => s.ActionLabel is "heapify_compare" or "swap")
            .ToArray();

        foreach (var step in heapifyActiveSteps)
        {
            step.Heap!.HeapBoundaryEnd.Should().Be(n - 1,
                because: "during build-heap the entire array is the heap — boundary is always n-1");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // STEP SEQUENCE / STRUCTURE
    // ─────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "UT-HEAP-44 - HeapSortEngine: first step action label is 'start'")]
    public void Run_FirstStep_ActionLabelIsStart()
    {
        var response = _sut.Run([4, 2, 7, 1]);

        response.Steps.First().ActionLabel.Should().Be("start",
            because: "the simulation must open with a 'start' sentinel step");
    }

    [Fact(DisplayName = "UT-HEAP-45 - HeapSortEngine: heapify_phase_start and heapify_phase_complete are always emitted")]
    public void Run_AlwaysEmits_HeapifyPhaseStartAndComplete()
    {
        var response = _sut.Run([3, 1, 2]);

        var labels = response.Steps.Select(s => s.ActionLabel).ToList();
        labels.Should().Contain("heapify_phase_start",
            because: "the heapify phase must be explicitly opened");
        labels.Should().Contain("heapify_phase_complete",
            because: "the heapify phase must be explicitly closed");
    }

    [Fact(DisplayName = "UT-HEAP-46 - HeapSortEngine: extraction_phase_start and extraction_phase_complete are always emitted")]
    public void Run_AlwaysEmits_ExtractionPhaseStartAndComplete()
    {
        var response = _sut.Run([3, 1, 2]);

        var labels = response.Steps.Select(s => s.ActionLabel).ToList();
        labels.Should().Contain("extraction_phase_start",
            because: "the extraction phase must be explicitly opened");
        labels.Should().Contain("extraction_phase_complete",
            because: "the extraction phase must be explicitly closed");
    }

    [Fact(DisplayName = "UT-HEAP-47 - HeapSortEngine: phase milestone steps appear in the correct sequence order")]
    public void Run_StepSequenceOrder_IsCorrect()
    {
        var response = _sut.Run([5, 3, 8, 1, 9]);

        var milestones = new[]
        {
            "start",
            "heapify_phase_start",
            "heapify_phase_complete",
            "extraction_phase_start",
            "extraction_phase_complete",
            "complete"
        };

        var milestoneIndices = milestones
            .Select(m => response.Steps.FindIndex(s => s.ActionLabel == m))
            .ToArray();

        for (var i = 1; i < milestoneIndices.Length; i++)
        {
            milestoneIndices[i].Should().BeGreaterThan(milestoneIndices[i - 1],
                because: $"'{milestones[i]}' must appear after '{milestones[i - 1]}'");
        }
    }

    [Fact(DisplayName = "UT-HEAP-48 - HeapSortEngine: extract_heapify_compare steps carry exactly two active indices")]
    public void Run_ExtractionCompareSteps_HaveTwoActiveIndices()
    {
        var response = _sut.Run([4, 10, 3, 5, 1]);

        var extractionCompares = response.Steps
            .Where(s => s.ActionLabel == "extract_heapify_compare")
            .ToArray();

        extractionCompares.Should().NotBeEmpty();
        foreach (var step in extractionCompares)
        {
            step.ActiveIndices.Should().HaveCount(2,
                because: "each sift-down comparison during extraction involves exactly a parent and a child index");
        }
    }

    [Fact(DisplayName = "UT-HEAP-49 - HeapSortEngine: single-element array produces only the six phase-milestone steps")]
    public void Run_SingleElementArray_StepSequenceIsMinimal()
    {
        var response = _sut.Run([42]);

        var expectedLabels = new[]
        {
            "start",
            "heapify_phase_start",
            "heapify_phase_complete",
            "extraction_phase_start",
            "extraction_phase_complete",
            "complete"
        };

        response.Steps.Should().HaveCount(expectedLabels.Length,
            because: "a single-element array needs only the six phase-milestone steps — no comparisons or swaps");
        response.Steps.Select(s => s.ActionLabel).Should().Equal(expectedLabels,
            because: "the exact step sequence must match the six milestones in order");
    }

    [Fact(DisplayName = "UT-HEAP-50 - HeapSortEngine: every step has a positive LineNumber")]
    public void Run_LineNumbers_AreNonZeroForEveryStep()
    {
        var response = _sut.Run([3, 1, 4, 1, 5]);

        foreach (var step in response.Steps)
        {
            step.LineNumber.Should().BeGreaterThan(0,
                because: $"step {step.StepNumber} ('{step.ActionLabel}') must map to a pseudocode line");
        }
    }
}
