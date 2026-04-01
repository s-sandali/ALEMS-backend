using backend.Models.Simulations;

namespace backend.Services.Simulations;

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
        var frameId = 1;
        var stack = new List<RecursionFrameModel>();
        var workingArray = array.ToArray();

        AddStep(steps, ref stepNumber, workingArray, [], 1, "start",
            BuildQuickSortMeta("start", recursionDepth: 0),
            "start", stack);

        if (workingArray.Length > 0)
        {
            QuickSortRecursive(workingArray, 0, workingArray.Length - 1,
                steps, ref stepNumber, stack, ref frameId, 0);
        }

        AddStep(steps, ref stepNumber, workingArray, [], 11, "complete",
            BuildQuickSortMeta("complete", recursionDepth: 0),
            "complete", stack);

        return new SimulationResponse
        {
            AlgorithmName = "Quick Sort",
            Steps = steps,
            TotalSteps = steps.Count
        };
    }

    private static void QuickSortRecursive(
        int[] array,
        int low,
        int high,
        List<SimulationStep> steps,
        ref int stepNumber,
        List<RecursionFrameModel> stack,
        ref int frameId,
        int depth)
    {
        var frame = new RecursionFrameModel
        {
            Id = frameId.ToString(),
            FunctionName = "quickSort",
            Depth = depth,
            State = "call",
            LeftIndex = low,
            RightIndex = high,
            Arguments = new RecursionFrameArguments { Left = low, Right = high }
        };
        frameId++;

        stack.Add(frame);

        AddStep(steps, ref stepNumber, array, [low, high], 2, "recursive_call",
            BuildQuickSortMeta("call", range: [low, high], recursionDepth: depth),
            "call", stack);

        if (low >= high)
        {
            frame.State = "return";
            frame.ReturnValue = "base_case";

            AddStep(steps, ref stepNumber, array, [low], 3, "base_case",
                BuildQuickSortMeta("baseCase", recursionDepth: depth),
                "return", stack);

            stack.RemoveAt(stack.Count - 1);
            return;
        }

        AddStep(steps, ref stepNumber, array, BuildIndicesList(low, high), 4, "partition_start",
            BuildQuickSortMeta("partitionStart", range: [low, high], recursionDepth: depth),
            "partition", stack);

        var pivotIndex = Partition(array, low, high, steps, ref stepNumber, stack, depth);

        AddStep(steps, ref stepNumber, array, [pivotIndex], 5, "pivot_positioned",
            BuildQuickSortMeta("pivotPositioned", pivotIndex: pivotIndex, range: [low, high], recursionDepth: depth),
            "pivotPlaced", stack);

        if (low < pivotIndex - 1)
        {
            AddStep(steps, ref stepNumber, array, BuildIndicesList(low, pivotIndex - 1), 6, "sort_left_start",
                BuildQuickSortMeta("sortLeftStart", range: [low, pivotIndex - 1], recursionDepth: depth),
                "call", stack);

            QuickSortRecursive(array, low, pivotIndex - 1, steps, ref stepNumber, stack, ref frameId, depth + 1);

            AddStep(steps, ref stepNumber, array, BuildIndicesList(low, pivotIndex - 1), 7, "sort_left_complete",
                BuildQuickSortMeta("sortLeftComplete", range: [low, pivotIndex - 1], recursionDepth: depth),
                "return", stack);
        }

        if (pivotIndex + 1 < high)
        {
            AddStep(steps, ref stepNumber, array, BuildIndicesList(pivotIndex + 1, high), 8, "sort_right_start",
                BuildQuickSortMeta("sortRightStart", range: [pivotIndex + 1, high], recursionDepth: depth),
                "call", stack);

            QuickSortRecursive(array, pivotIndex + 1, high, steps, ref stepNumber, stack, ref frameId, depth + 1);

            AddStep(steps, ref stepNumber, array, BuildIndicesList(pivotIndex + 1, high), 9, "sort_right_complete",
                BuildQuickSortMeta("sortRightComplete", range: [pivotIndex + 1, high], recursionDepth: depth),
                "return", stack);
        }

        frame.State = "return";
        frame.ReturnValue = $"pivot={pivotIndex}";

        AddStep(steps, ref stepNumber, array, [low, high], 10, "return",
            BuildQuickSortMeta("return", range: [low, high], recursionDepth: depth),
            "return", stack);

        stack.RemoveAt(stack.Count - 1);
    }

    private static int Partition(
        int[] array,
        int low,
        int high,
        List<SimulationStep> steps,
        ref int stepNumber,
        List<RecursionFrameModel> stack,
        int depth)
    {
        var pivot = array[high];

        AddStep(steps, ref stepNumber, array, [high], 11, "pivot_select",
            BuildQuickSortMeta("pivotSelect", pivot: pivot, pivotIndex: high, range: [low, high], recursionDepth: depth),
            "pivotSelect", stack);

        var i = low - 1;

        for (var j = low; j < high; j++)
        {
            AddStep(steps, ref stepNumber, array, [j, high], 12, "compare",
                BuildQuickSortMeta("compare", pivot: pivot, pivotIndex: high, range: [low, high], recursionDepth: depth),
                "compare", stack);

            if (array[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    (array[i], array[j]) = (array[j], array[i]);

                    AddStep(steps, ref stepNumber, array, [i, j], 13, "swap",
                        BuildQuickSortMeta("swap", pivot: pivot, range: [low, high], recursionDepth: depth),
                        "swap", stack);
                }
            }
        }

        if (i + 1 != high)
        {
            (array[i + 1], array[high]) = (array[high], array[i + 1]);

            AddStep(steps, ref stepNumber, array, [i + 1, high], 14, "pivot_swap",
                BuildQuickSortMeta("pivotSwap", pivot: pivot, range: [low, high], recursionDepth: depth),
                "swap", stack);
        }

        AddStep(steps, ref stepNumber, array, [i + 1], 15, "pivotPlaced",
            BuildQuickSortMeta("pivotPlaced", pivot: pivot, pivotIndex: i + 1, range: [low, high], recursionDepth: depth),
            "return", stack);

        return i + 1;
    }

    private static int[] BuildIndicesList(int low, int high)
    {
        var indices = new List<int>();
        for (var i = low; i <= high; i++)
        {
            indices.Add(i);
        }
        return indices.ToArray();
    }

    private static RecursionStepModel BuildRecursionModel(List<RecursionFrameModel> stack, string? recursionEvent)
    {
        var currentFrame = stack.Count > 0 ? stack[^1] : null;

        return new RecursionStepModel
        {
            Event = recursionEvent,
            State = recursionEvent,
            Depth = currentFrame?.Depth ?? 0,
            CurrentFrameId = currentFrame?.Id,
            Stack = [.. stack]
        };
    }

    private static void AddStep(
        List<SimulationStep> steps,
        ref int stepNumber,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
        QuickSortStepModel? quickSort,
        string? recursionEvent,
        List<RecursionFrameModel> stack)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            QuickSort = quickSort,
            Recursion = BuildRecursionModel(stack, recursionEvent)
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