using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Insertion Sort.
/// </summary>
public class InsertionSortSimulationEngine : IAlgorithmSimulationEngine
{
    public bool CanHandle(string algorithm)
    {
        if (string.IsNullOrWhiteSpace(algorithm))
        {
            return false;
        }

        var normalized = algorithm.Trim().ToLowerInvariant();
        return normalized is "insertion_sort" or "insertion-sort";
    }

    public SimulationResponse Run(int[] array, int? targetValue = null)
    {
        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        AddStep(steps, ref stepNumber, array, [], 1, "start");

        for (var i = 1; i < array.Length; i++)
        {
            var key = array[i];
            var j = i - 1;

            AddStep(steps, ref stepNumber, array, [i], 2, "pick_key");

            while (j >= 0)
            {
                AddStep(steps, ref stepNumber, array, [j, j + 1], 3, "compare");

                if (array[j] <= key)
                {
                    break;
                }

                array[j + 1] = array[j];
                AddStep(steps, ref stepNumber, array, [j, j + 1], 4, "shift");
                j--;
            }

            array[j + 1] = key;
            AddStep(steps, ref stepNumber, array, [j + 1], 5, "insert");
        }

        AddStep(steps, ref stepNumber, array, [], 6, "complete");

        return new SimulationResponse
        {
            AlgorithmName = "Insertion Sort",
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