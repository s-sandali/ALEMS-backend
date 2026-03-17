using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Bubble Sort.
/// </summary>
public class BubbleSortSimulationEngine : IAlgorithmSimulationEngine
{
    public bool CanHandle(string algorithm) =>
        algorithm == "bubble_sort" || algorithm == "bubble-sort";

    public SimulationResponse Run(int[] array)
    {
        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        AddStep(steps, ref stepNumber, array, [], 1, "start");

        for (var i = 0; i < array.Length - 1; i++)
        {
            var swapped = false;
            AddStep(steps, ref stepNumber, array, [i], 2, "pass_start");

            for (var j = 0; j < array.Length - i - 1; j++)
            {
                AddStep(steps, ref stepNumber, array, [j, j + 1], 3, "compare");

                if (array[j] > array[j + 1])
                {
                    (array[j], array[j + 1]) = (array[j + 1], array[j]);
                    swapped = true;
                    AddStep(steps, ref stepNumber, array, [j, j + 1], 4, "swap");
                }
            }

            if (!swapped)
            {
                AddStep(steps, ref stepNumber, array, [], 5, "early_exit");
                break;
            }
        }

        AddStep(steps, ref stepNumber, array, [], 6, "complete");

        return new SimulationResponse
        {
            AlgorithmName = "Bubble Sort",
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
