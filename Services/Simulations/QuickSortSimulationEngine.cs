using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Quick Sort using recursive approach.
/// </summary>
public class QuickSortSimulationEngine : IAlgorithmSimulationEngine
{
    public bool CanHandle(string algorithm)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            return false;
        }

        var normalized = algorithm.Trim().ToLowerInvariant();
        return normalized is "quick_sort" or "quick-sort";
    }

    public SimulationResponse Run(int[] array, int? targetValue = null)
    {
        var steps = new List<SimulationStep>();
        var stepNumber = 1;
        var workingArray = array.ToArray();

        AddStep(
            steps,
            ref stepNumber,
            workingArray,
            [],
            1,
            "start",
            BuildQuickSortMeta("start", recursionDepth: 0));

        QuickSortRecursive(workingArray, 0, workingArray.Length - 1, steps, ref stepNumber, depth: 0);

        AddStep(
            steps,
            ref stepNumber,
            workingArray,
            [],
            11,
            "complete",
            BuildQuickSortMeta("complete", recursionDepth: 0));

        return new SimulationResponse
        {
            AlgorithmName = "Quick Sort",
            Steps = steps,
            TotalSteps = steps.Count
        };
    }

    /// <summary>
    /// Recursively sorts the array using Quick Sort algorithm.
    /// </summary>
    private static void QuickSortRecursive(
        int[] array,
        int low,
        int high,
        List<SimulationStep> steps,
        ref int stepNumber,
        int depth = 0)
    {
        if (low < high)
        {
            var activeIndices = BuildIndicesList(low, high);

            AddStep(
                steps,
                ref stepNumber,
                array,
                activeIndices,
                2,
                "partition_start",
                BuildQuickSortMeta("partitionStart", range: [low, high], recursionDepth: depth));

            var pivotIndex = Partition(array, low, high, steps, ref stepNumber, depth);

            AddStep(
                steps,
                ref stepNumber,
                array,
                [pivotIndex],
                3,
                "pivot_positioned",
                BuildQuickSortMeta("pivotPositioned", pivotIndex: pivotIndex, range: [low, high], recursionDepth: depth));

            // Left recursion
            if (low < pivotIndex - 1)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    array,
                    BuildIndicesList(low, pivotIndex - 1),
                    9,
                    "sort_left_start",
                    BuildQuickSortMeta("sortLeftStart", range: [low, pivotIndex - 1], recursionDepth: depth));

                QuickSortRecursive(array, low, pivotIndex - 1, steps, ref stepNumber, depth + 1);

                AddStep(
                    steps,
                    ref stepNumber,
                    array,
                    BuildIndicesList(low, pivotIndex - 1),
                    10,
                    "sort_left_complete",
                    BuildQuickSortMeta("sortLeftComplete", range: [low, pivotIndex - 1], recursionDepth: depth));
            }

            // Right recursion
            if (pivotIndex + 1 < high)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    array,
                    BuildIndicesList(pivotIndex + 1, high),
                    9,
                    "sort_right_start",
                    BuildQuickSortMeta("sortRightStart", range: [pivotIndex + 1, high], recursionDepth: depth));

                QuickSortRecursive(array, pivotIndex + 1, high, steps, ref stepNumber, depth + 1);

                AddStep(
                    steps,
                    ref stepNumber,
                    array,
                    BuildIndicesList(pivotIndex + 1, high),
                    10,
                    "sort_right_complete",
                    BuildQuickSortMeta("sortRightComplete", range: [pivotIndex + 1, high], recursionDepth: depth));
            }
        }
    }

    /// <summary>
    /// Partitions the array around a pivot element using Lomuto partition scheme.
    /// </summary>
    private static int Partition(
        int[] array,
        int low,
        int high,
        List<SimulationStep> steps,
        ref int stepNumber,
        int depth = 0)
    {
        var pivot = array[high];

        AddStep(
            steps,
            ref stepNumber,
            array,
            [high],
            4,
            "pivot_select",
            BuildQuickSortMeta("pivotSelect", pivot: pivot, pivotIndex: high, range: [low, high], recursionDepth: depth));

        var i = low - 1;

        for (var j = low; j < high; j++)
        {
            AddStep(
                steps,
                ref stepNumber,
                array,
                [j, high],
                5,
                "compare",
                BuildQuickSortMeta("compare", pivot: pivot, pivotIndex: high, range: [low, high], recursionDepth: depth));

            if (array[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    (array[i], array[j]) = (array[j], array[i]);

                    AddStep(
                        steps,
                        ref stepNumber,
                        array,
                        [i, j],
                        6,
                        "swap",
                        BuildQuickSortMeta("swap", pivot: pivot, range: [low, high], recursionDepth: depth));
                }
            }
        }

        if (i + 1 != high)
        {
            (array[i + 1], array[high]) = (array[high], array[i + 1]);

            AddStep(
                steps,
                ref stepNumber,
                array,
                [i + 1, high],
                7,
                "pivot_swap",
                BuildQuickSortMeta("pivotSwap", pivot: pivot, range: [low, high], recursionDepth: depth));
        }

        AddStep(
            steps,
            ref stepNumber,
            array,
            [i + 1],
            8,
            "pivotPlaced",
            BuildQuickSortMeta("pivotPlaced", pivot: pivot, pivotIndex: i + 1, range: [low, high], recursionDepth: depth));

        return i + 1;
    }

    /// <summary>
    /// Helper method to build array of indices from low to high.
    /// </summary>
    private static int[] BuildIndicesList(int low, int high)
    {
        var indices = new List<int>();
        for (var i = low; i <= high; i++)
        {
            indices.Add(i);
        }
        return indices.ToArray();
    }

    private static void AddStep(
        List<SimulationStep> steps,
        ref int stepNumber,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
        QuickSortStepModel? quickSort)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            QuickSort = quickSort
        });
    }

    private static QuickSortStepModel BuildQuickSortMeta(
        string type,
        int? pivot = null,
        int? pivotIndex = null,
        int[]? range = null,
        int? recursionDepth = null)
    {
        return new QuickSortStepModel
        {
            Type = type,
            Pivot = pivot,
            PivotIndex = pivotIndex,
            Range = range ?? [],
            RecursionDepth = recursionDepth
        };
    }
}