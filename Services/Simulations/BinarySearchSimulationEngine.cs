using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Binary Search.
/// </summary>
public class BinarySearchSimulationEngine : IAlgorithmSimulationEngine
{
    public bool CanHandle(string algorithm) =>
        algorithm == "binary_search" || algorithm == "binary-search";

    public SimulationResponse Run(int[] array)
    {
        var values = array
            .OrderBy(value => value)
            .ToArray();

        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        if (values.Length == 0)
        {
            AddStep(steps, ref stepNumber, values, [], 8, "not_found");

            return new SimulationResponse
            {
                AlgorithmName = "Binary Search",
                Steps = steps,
                TotalSteps = steps.Count
            };
        }

        var target = values[^1];
        var low = 0;
        var high = values.Length - 1;

        AddStep(steps, ref stepNumber, values, [low, high], 1, "start");

        while (low <= high)
        {
            var mid = (low + high) / 2;
            AddStep(steps, ref stepNumber, values, [mid], 4, "midpoint_pick");

            if (values[mid] == target)
            {
                AddStep(steps, ref stepNumber, values, [mid], 5, "found");
                break;
            }

            if (values[mid] < target)
            {
                AddStep(steps, ref stepNumber, values, [mid], 6, "discard_left");
                low = mid + 1;
                continue;
            }

            AddStep(steps, ref stepNumber, values, [mid], 7, "discard_right");
            high = mid - 1;
        }

        if (steps.Count == 0 || steps[^1].ActionLabel != "found")
        {
            AddStep(steps, ref stepNumber, values, [], 8, "not_found");
        }

        return new SimulationResponse
        {
            AlgorithmName = "Binary Search",
            Steps = steps,
            TotalSteps = steps.Count
        };
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