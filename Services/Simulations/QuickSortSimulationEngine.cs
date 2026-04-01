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
        var frameId = 1;
        var stack = new List<RecursionFrameModel>();
        var workingArray = array.ToArray();

        AddStep(steps, ref stepNumber, workingArray, [], 1, "start", "start", stack);

        if (workingArray.Length > 0)
        {
            QuickSortRecursive(workingArray, 0, workingArray.Length - 1, steps, ref stepNumber, stack, ref frameId, 0);
        }

        AddStep(steps, ref stepNumber, workingArray, [], 11, "complete", "complete", stack);

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
        AddStep(steps, ref stepNumber, array, [low, high], 2, "recursive_call", "call", stack);

        if (low >= high)
        {
            frame.State = "return";
            frame.ReturnValue = "base_case";
            AddStep(steps, ref stepNumber, array, [low], 3, "base_case", "return", stack);
            stack.RemoveAt(stack.Count - 1);
            return;
        }

        frame.State = "partition";
        AddStep(steps, ref stepNumber, array, BuildIndicesList(low, high), 4, "partition_start", "partition", stack);

        var pivotIndex = Partition(array, low, high, steps, ref stepNumber, stack);

        frame.State = "pivot_placed";
        AddStep(steps, ref stepNumber, array, [pivotIndex], 5, "pivot_positioned", "pivotPlaced", stack);

        if (low < pivotIndex - 1)
        {
            AddStep(steps, ref stepNumber, array, BuildIndicesList(low, pivotIndex - 1), 6, "sort_left_start", "call", stack);
            QuickSortRecursive(array, low, pivotIndex - 1, steps, ref stepNumber, stack, ref frameId, depth + 1);
            AddStep(steps, ref stepNumber, array, BuildIndicesList(low, pivotIndex - 1), 7, "sort_left_complete", "return", stack);
        }

        if (pivotIndex + 1 < high)
        {
            AddStep(steps, ref stepNumber, array, BuildIndicesList(pivotIndex + 1, high), 8, "sort_right_start", "call", stack);
            QuickSortRecursive(array, pivotIndex + 1, high, steps, ref stepNumber, stack, ref frameId, depth + 1);
            AddStep(steps, ref stepNumber, array, BuildIndicesList(pivotIndex + 1, high), 9, "sort_right_complete", "return", stack);
        }

        frame.State = "return";
        frame.ReturnValue = $"pivot={pivotIndex}";
        AddStep(steps, ref stepNumber, array, [low, high], 10, "return", "return", stack);
        stack.RemoveAt(stack.Count - 1);
    }

    /// <summary>
    /// Partitions the array around a pivot element using the Lomuto partition scheme.
    /// </summary>
    private static int Partition(
        int[] array,
        int low,
        int high,
        List<SimulationStep> steps,
        ref int stepNumber,
        List<RecursionFrameModel> stack)
    {
        var pivot = array[high];
        AddStep(steps, ref stepNumber, array, [high], 11, "pivot_select", "pivotSelect", stack);

        var i = low - 1;

        for (var j = low; j < high; j++)
        {
            AddStep(steps, ref stepNumber, array, [j, high], 12, "compare", "compare", stack);

            if (array[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    (array[i], array[j]) = (array[j], array[i]);
                    AddStep(steps, ref stepNumber, array, [i, j], 13, "swap", "swap", stack);
                }
            }
        }

        if (i + 1 != high)
        {
            (array[i + 1], array[high]) = (array[high], array[i + 1]);
            AddStep(steps, ref stepNumber, array, [i + 1, high], 14, "pivot_swap", "swap", stack);
        }

        AddStep(steps, ref stepNumber, array, [i + 1], 15, "partition_complete", "return", stack);
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

    private static RecursionStepModel BuildRecursionModel(List<RecursionFrameModel> stack, string? recursionEvent)
    {
        var currentFrame = stack.Count > 0 ? stack[^1] : null;

        return new RecursionStepModel
        {
            Event = recursionEvent,
            State = recursionEvent,
            Depth = currentFrame?.Depth ?? 0,
            CurrentFrameId = currentFrame?.Id,
            Stack =
            [
                .. stack.Select(frame => new RecursionFrameModel
                {
                    Id = frame.Id,
                    FunctionName = frame.FunctionName,
                    Depth = frame.Depth,
                    State = frame.State,
                    LeftIndex = frame.LeftIndex,
                    RightIndex = frame.RightIndex,
                    MidpointIndex = frame.MidpointIndex,
                    Arguments = new RecursionFrameArguments
                    {
                        Left = frame.Arguments.Left,
                        Right = frame.Arguments.Right
                    },
                    ReturnValue = frame.ReturnValue
                })
            ]
        };
    }

    private static void AddStep(
        List<SimulationStep> steps,
        ref int stepNumber,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
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
            Recursion = BuildRecursionModel(stack, recursionEvent)
        });
    }
}
