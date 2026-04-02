using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Quick Sort using recursive approach.
/// </summary>
public class QuickSortSimulationEngine : IAlgorithmSimulationEngine
{
    private static class RecursionEvent
    {
        public const string Start = "start";
        public const string Call = "call";
        public const string Return = "return";
        public const string PartitionPhase = "partition";
        public const string PivotPlaced = "pivot_placed";
        public const string PivotSelect = "pivot_select";
        public const string Compare = "compare";
        public const string Swap = "swap";
        public const string Complete = "complete";
    }

    /// <summary>
    /// Mutable context shared across all recursive calls to avoid excessive method parameters.
    /// Holds step accumulation state alongside recursion tracking.
    /// </summary>
    private sealed class RecursionContext
    {
        public List<SimulationStep> Steps { get; } = [];
        public int StepNumber { get; set; } = 1;
        public List<RecursionFrameModel> Stack { get; } = [];
        public int NextFrameId { get; set; } = 1;
    }

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
        var ctx = new RecursionContext();
        var workingArray = array.ToArray();

        AddStep(ctx, workingArray, [], 1, "start",
            BuildQuickSortMeta("start", recursionDepth: 0),
            RecursionEvent.Start);

        if (workingArray.Length > 0)
        {
            QuickSortRecursive(workingArray, 0, workingArray.Length - 1, ctx, 0);
        }

        // Line 16 avoids collision with Partition's line 11 (pivot_select)
        AddStep(ctx, workingArray, [], 16, "complete",
            BuildQuickSortMeta("complete", recursionDepth: 0),
            RecursionEvent.Complete);

        return new SimulationResponse
        {
            AlgorithmName = "Quick Sort",
            Steps = ctx.Steps,
            TotalSteps = ctx.Steps.Count
        };
    }

    /// <summary>
    /// Recursively sorts the array using Quick Sort algorithm.
    /// </summary>
    private static void QuickSortRecursive(int[] array, int low, int high, RecursionContext ctx, int depth)
    {
        var frame = new RecursionFrameModel
        {
            Id = ctx.NextFrameId++,
            FunctionName = "quickSort",
            Depth = depth,
            State = RecursionEvent.Call,
            LeftIndex = low,
            RightIndex = high
        };

        ctx.Stack.Add(frame);

        AddStep(ctx, array, [low, high], 2, "recursive_call",
            BuildQuickSortMeta("call", range: [low, high], recursionDepth: depth),
            RecursionEvent.Call);

        if (low >= high)
        {
            frame.State = RecursionEvent.Return;
            frame.ReturnValue = "base_case";

            AddStep(ctx, array, [low], 3, "base_case",
                BuildQuickSortMeta("base_case", recursionDepth: depth),
                RecursionEvent.Return);

            ctx.Stack.RemoveAt(ctx.Stack.Count - 1);
            return;
        }

        AddStep(ctx, array, Enumerable.Range(low, high - low + 1).ToArray(), 4, "partition_start",
            BuildQuickSortMeta("partition_start", range: [low, high], recursionDepth: depth),
            RecursionEvent.PartitionPhase);

        var pivotIndex = Partition(array, low, high, ctx, depth);

        AddStep(ctx, array, [pivotIndex], 5, "pivot_positioned",
            BuildQuickSortMeta("pivot_positioned", pivotIndex: pivotIndex, range: [low, high], recursionDepth: depth),
            RecursionEvent.PivotPlaced);

        // Recurse on left partition whenever at least one element exists (includes single-element base case)
        if (low < pivotIndex)
        {
            AddStep(ctx, array, Enumerable.Range(low, pivotIndex - low).ToArray(), 6, "sort_left_start",
                BuildQuickSortMeta("sort_left_start", range: [low, pivotIndex - 1], recursionDepth: depth),
                RecursionEvent.Call);

            QuickSortRecursive(array, low, pivotIndex - 1, ctx, depth + 1);

            AddStep(ctx, array, Enumerable.Range(low, pivotIndex - low).ToArray(), 7, "sort_left_complete",
                BuildQuickSortMeta("sort_left_complete", range: [low, pivotIndex - 1], recursionDepth: depth),
                RecursionEvent.Return);
        }

        // Recurse on right partition whenever at least one element exists (includes single-element base case)
        if (pivotIndex < high)
        {
            AddStep(ctx, array, Enumerable.Range(pivotIndex + 1, high - pivotIndex).ToArray(), 8, "sort_right_start",
                BuildQuickSortMeta("sort_right_start", range: [pivotIndex + 1, high], recursionDepth: depth),
                RecursionEvent.Call);

            QuickSortRecursive(array, pivotIndex + 1, high, ctx, depth + 1);

            AddStep(ctx, array, Enumerable.Range(pivotIndex + 1, high - pivotIndex).ToArray(), 9, "sort_right_complete",
                BuildQuickSortMeta("sort_right_complete", range: [pivotIndex + 1, high], recursionDepth: depth),
                RecursionEvent.Return);
        }

        frame.State = RecursionEvent.Return;
        frame.ReturnValue = $"pivot={pivotIndex}";

        AddStep(ctx, array, [low, high], 10, "return",
            BuildQuickSortMeta("return", range: [low, high], recursionDepth: depth),
            RecursionEvent.Return);

        ctx.Stack.RemoveAt(ctx.Stack.Count - 1);
    }

    /// <summary>
    /// Partitions the array around a pivot element using Lomuto partition scheme.
    /// Pseudocode lines 11–15 cover the partition subroutine.
    /// </summary>
    private static int Partition(int[] array, int low, int high, RecursionContext ctx, int depth)
    {
        var pivot = array[high];

        AddStep(ctx, array, [high], 11, "pivot_select",
            BuildQuickSortMeta("pivot_select", pivot: pivot, pivotIndex: high, range: [low, high], recursionDepth: depth),
            RecursionEvent.PivotSelect);

        var i = low - 1;

        for (var j = low; j < high; j++)
        {
            AddStep(ctx, array, [j, high], 12, "compare",
                BuildQuickSortMeta("compare", pivot: pivot, pivotIndex: high, range: [low, high], recursionDepth: depth),
                RecursionEvent.Compare);

            if (array[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    (array[i], array[j]) = (array[j], array[i]);

                    AddStep(ctx, array, [i, j], 13, "swap",
                        BuildQuickSortMeta("swap", pivot: pivot, range: [low, high], recursionDepth: depth),
                        RecursionEvent.Swap);
                }
            }
        }

        if (i + 1 != high)
        {
            (array[i + 1], array[high]) = (array[high], array[i + 1]);

            AddStep(ctx, array, [i + 1, high], 14, "pivot_swap",
                BuildQuickSortMeta("pivot_swap", pivot: pivot, range: [low, high], recursionDepth: depth),
                RecursionEvent.Swap);
        }

        AddStep(ctx, array, [i + 1], 15, "pivot_placed",
            BuildQuickSortMeta("pivot_placed", pivot: pivot, pivotIndex: i + 1, range: [low, high], recursionDepth: depth),
            RecursionEvent.Return);

        return i + 1;
    }

    private static RecursionStepModel BuildRecursionModel(RecursionContext ctx, string? state)
    {
        var currentFrame = ctx.Stack.Count > 0 ? ctx.Stack[^1] : null;

        return new RecursionStepModel
        {
            State = state,
            Depth = currentFrame?.Depth ?? 0,
            CurrentFrameId = currentFrame?.Id,
            Stack = [.. ctx.Stack]
        };
    }

    private static void AddStep(
        RecursionContext ctx,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
        QuickSortStepModel? quickSort,
        string? state)
    {
        ctx.Steps.Add(new SimulationStep
        {
            StepNumber = ctx.StepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            QuickSort = quickSort,
            Recursion = BuildRecursionModel(ctx, state)
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
