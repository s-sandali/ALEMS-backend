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
}
