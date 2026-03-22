using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Binary Search.
/// </summary>
public class BinarySearchSimulationEngine : IAlgorithmSimulationEngine
{
    public bool CanHandle(string algorithm) =>
        algorithm == "binary_search" || algorithm == "binary-search";

    public SimulationResponse Run(int[] array, int? targetValue = null)
    {
        var values = array
            .OrderBy(value => value)
            .ToArray();

        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        if (values.Length == 0)
        {
            AddStep(
                steps,
                ref stepNumber,
                values,
                [],
                8,
                "not_found",
                BuildSearchStep(0, -1, null, "not_found"));

            return new SimulationResponse
            {
                AlgorithmName = "Binary Search",
                Steps = steps,
                TotalSteps = steps.Count,
                TargetValue = targetValue
            };
        }

        var target = targetValue ?? values[^1];
        var low = 0;
        var high = values.Length - 1;

        AddStep(
            steps,
            ref stepNumber,
            values,
            [low, high],
            1,
            "start",
            BuildSearchStep(low, high, null, "start"));

        while (low <= high)
        {
            var mid = (low + high) / 2;
            AddStep(
                steps,
                ref stepNumber,
                values,
                [mid],
                4,
                "midpoint_pick",
                BuildSearchStep(low, high, mid, "midpoint_pick"));

            if (values[mid] == target)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [mid],
                    5,
                    "found",
                    BuildSearchStep(low, high, mid, "found"));
                break;
            }

            if (values[mid] < target)
            {
                var discardStart = low;
                var discardEnd = mid;
                var discardedIndices = BuildDiscardedIndices(discardStart, discardEnd);
                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [mid],
                    6,
                    "discard_left",
                    BuildSearchStep(
                        low,
                        high,
                        mid,
                        "discard_left",
                        "left",
                        discardStart,
                        discardEnd,
                        discardedIndices));
                low = mid + 1;
                continue;
            }

            var rightDiscardStart = mid;
            var rightDiscardEnd = high;
            var rightDiscardedIndices = BuildDiscardedIndices(rightDiscardStart, rightDiscardEnd);
            AddStep(
                steps,
                ref stepNumber,
                values,
                [mid],
                7,
                "discard_right",
                BuildSearchStep(
                    low,
                    high,
                    mid,
                    "discard_right",
                    "right",
                    rightDiscardStart,
                    rightDiscardEnd,
                    rightDiscardedIndices));
            high = mid - 1;
        }

        if (steps.Count == 0 || steps[^1].ActionLabel != "found")
        {
            AddStep(
                steps,
                ref stepNumber,
                values,
                [],
                8,
                "not_found",
                BuildSearchStep(low, high, null, "not_found"));
        }

        return new SimulationResponse
        {
            AlgorithmName = "Binary Search",
            Steps = steps,
            TotalSteps = steps.Count,
            TargetValue = target
        };
    }

    private static void AddStep(
        List<SimulationStep> steps,
        ref int stepNumber,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
        SearchStepModel? searchStep)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            Search = searchStep
        });
    }

    private static SearchStepModel BuildSearchStep(
        int low,
        int high,
        int? midpoint,
        string state,
        string? discardedSide = null,
        int? discardStartIndex = null,
        int? discardEndIndex = null,
        int[]? discardedIndices = null)
    {
        return new SearchStepModel
        {
            LowIndex = low,
            HighIndex = high,
            MidpointIndex = midpoint,
            State = state,
            DiscardedSide = discardedSide,
            DiscardStartIndex = discardStartIndex,
            DiscardEndIndex = discardEndIndex,
            DiscardedIndices = discardedIndices ?? []
        };
    }

    private static int[] BuildDiscardedIndices(int start, int end)
    {
        if (start > end)
        {
            return [];
        }

        return Enumerable.Range(start, end - start + 1).ToArray();
    }
}