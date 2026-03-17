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
            AddStep(steps, ref stepNumber, array, [i], 2, "pass_start");

            for (var j = 0; j < array.Length - i - 1; j++)
            {
                AddStep(steps, ref stepNumber, array, [j, j + 1], 3, "compare");

                if (array[j] > array[j + 1])
                {
                    (array[j], array[j + 1]) = (array[j + 1], array[j]);
                    AddStep(steps, ref stepNumber, array, [j, j + 1], 4, "swap");
                }
            }

            AddStep(steps, ref stepNumber, array, [array.Length - i - 1], 5, "sorted");
        }

        AddStep(steps, ref stepNumber, array, [], 6, "complete");

        return new SimulationResponse
        {
            AlgorithmName = "Bubble Sort",
            Steps = steps,
            TotalSteps = steps.Count
        };
    }

    public SimulationValidationResponse ValidateStep(int[] currentArray, string actionType, int[] indices)
    {
        var normalizedAction = actionType.Trim().ToLowerInvariant();
        var expectedSwapIndex = FindNextSwapIndex(currentArray);

        if (expectedSwapIndex is null)
        {
            var isCompletionAction = normalizedAction is "found" or "complete" or "sorted";
            return new SimulationValidationResponse
            {
                Correct = isCompletionAction,
                NextState = currentArray.ToArray()
            };
        }

        var leftIndex = expectedSwapIndex.Value;
        var expectedIndices = new[] { leftIndex, leftIndex + 1 };
        var isCorrectAction =
            normalizedAction == "swap" &&
            indices.Length == 2 &&
            indices[0] == expectedIndices[0] &&
            indices[1] == expectedIndices[1];

        if (!isCorrectAction)
        {
            return new SimulationValidationResponse
            {
                Correct = false,
                NextState = currentArray.ToArray()
            };
        }

        var nextState = currentArray.ToArray();
        (nextState[leftIndex], nextState[leftIndex + 1]) = (nextState[leftIndex + 1], nextState[leftIndex]);

        return new SimulationValidationResponse
        {
            Correct = true,
            NextState = nextState
        };
    }

    private static int? FindNextSwapIndex(int[] array)
    {
        for (var i = 0; i < array.Length - 1; i++)
        {
            if (array[i] > array[i + 1])
            {
                return i;
            }
        }

        return null;
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
