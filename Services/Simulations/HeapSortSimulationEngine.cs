using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Heap Sort.
/// Contract: includes separated heapify and extraction phases.
/// </summary>
public class HeapSortSimulationEngine : IAlgorithmSimulationEngine
{
    private const string AlgorithmDisplayName = "Heap Sort";

    private static class PseudocodeLine
    {
        public const int Start = 1;
        public const int HeapifyPhaseStart = 2;
        public const int HeapifyCompare = 3;
        public const int HeapifySwap = 4;
        public const int HeapifyPhaseComplete = 5;
        public const int ExtractionPhaseStart = 6;
        public const int ExtractRootSwap = 7;
        public const int ExtractSiftCompare = 8;
        public const int ExtractSiftSwap = 9;
        public const int Complete = 10;
    }

    public bool CanHandle(string algorithm)
    {
        var normalized = algorithm.Trim().ToLowerInvariant();
        return normalized is "heap_sort" or "heap-sort";
    }

    public SimulationResponse Run(int[] array, int? targetValue = null)
    {
        var values = array.ToArray();
        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        AddStep(steps, ref stepNumber, values, [], PseudocodeLine.Start, "start");

        BuildMaxHeap(values, steps, ref stepNumber);
        ExtractFromHeap(values, steps, ref stepNumber);

        AddStep(steps, ref stepNumber, values, [], PseudocodeLine.Complete, "complete");

        return new SimulationResponse
        {
            AlgorithmName = AlgorithmDisplayName,
            Steps = steps,
            TotalSteps = steps.Count
        };
    }

    private static void BuildMaxHeap(int[] values, List<SimulationStep> steps, ref int stepNumber)
    {
        AddStep(steps, ref stepNumber, values, [], PseudocodeLine.HeapifyPhaseStart, "heapify_phase_start");

        if (values.Length <= 1)
        {
            AddStep(steps, ref stepNumber, values, [], PseudocodeLine.HeapifyPhaseComplete, "heapify_phase_complete");
            return;
        }

        for (var parent = (values.Length / 2) - 1; parent >= 0; parent--)
        {
            SiftDown(
                values,
                heapSize: values.Length,
                rootIndex: parent,
                steps,
                ref stepNumber,
                compareLine: PseudocodeLine.HeapifyCompare,
                swapLine: PseudocodeLine.HeapifySwap,
                compareLabel: "heapify_compare",
                swapLabel: "heapify_swap");
        }

        AddStep(steps, ref stepNumber, values, [], PseudocodeLine.HeapifyPhaseComplete, "heapify_phase_complete");
    }

    private static void ExtractFromHeap(int[] values, List<SimulationStep> steps, ref int stepNumber)
    {
        AddStep(steps, ref stepNumber, values, [], PseudocodeLine.ExtractionPhaseStart, "extraction_phase_start");

        if (values.Length <= 1)
        {
            AddStep(steps, ref stepNumber, values, [], PseudocodeLine.Complete, "extraction_phase_complete");
            return;
        }

        for (var end = values.Length - 1; end > 0; end--)
        {
            (values[0], values[end]) = (values[end], values[0]);
            AddStep(steps, ref stepNumber, values, [0, end], PseudocodeLine.ExtractRootSwap, "extract_swap_root");

            SiftDown(
                values,
                heapSize: end,
                rootIndex: 0,
                steps,
                ref stepNumber,
                compareLine: PseudocodeLine.ExtractSiftCompare,
                swapLine: PseudocodeLine.ExtractSiftSwap,
                compareLabel: "extract_heapify_compare",
                swapLabel: "extract_heapify_swap");
        }

        AddStep(steps, ref stepNumber, values, [], PseudocodeLine.Complete, "extraction_phase_complete");
    }

    private static void SiftDown(
        int[] values,
        int heapSize,
        int rootIndex,
        List<SimulationStep> steps,
        ref int stepNumber,
        int compareLine,
        int swapLine,
        string compareLabel,
        string swapLabel)
    {
        var root = rootIndex;

        while (true)
        {
            var left = 2 * root + 1;
            if (left >= heapSize)
            {
                return;
            }

            var right = left + 1;
            var largest = root;

            AddStep(steps, ref stepNumber, values, [root, left], compareLine, compareLabel);
            if (values[left] > values[largest])
            {
                largest = left;
            }

            if (right < heapSize)
            {
                AddStep(steps, ref stepNumber, values, [largest, right], compareLine, compareLabel);
                if (values[right] > values[largest])
                {
                    largest = right;
                }
            }

            if (largest == root)
            {
                return;
            }

            (values[root], values[largest]) = (values[largest], values[root]);
            AddStep(steps, ref stepNumber, values, [root, largest], swapLine, swapLabel);
            root = largest;
        }
    }

    private static void AddStep(
        List<SimulationStep> steps,
        ref int stepNumber,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel
        });
    }
}
