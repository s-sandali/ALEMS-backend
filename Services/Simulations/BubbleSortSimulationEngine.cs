using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Bubble Sort.
/// </summary>
public class BubbleSortSimulationEngine : IAlgorithmSimulationEngine
{
    public bool CanHandle(string algorithm) =>
        algorithm == "bubble_sort" || algorithm == "bubble-sort";

    public SimulationResponse Run(int[] array, int? targetValue = null)
    {
        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        AddStep(steps, ref stepNumber, array, [], 1, "start", BuildBubblePartition(0, 0, array.Length));

        for (var i = 0; i < array.Length - 1; i++)
        {
            var swapped = false;
            AddStep(steps, ref stepNumber, array, [i], 2, "pass_start", BuildBubblePartition(i, i + 1, array.Length));

            for (var j = 0; j < array.Length - i - 1; j++)
            {
                AddStep(steps, ref stepNumber, array, [j, j + 1], 3, "compare", BuildBubblePartition(i, i + 1, array.Length));

                if (array[j] > array[j + 1])
                {
                    (array[j], array[j + 1]) = (array[j + 1], array[j]);
                    swapped = true;
                    AddStep(steps, ref stepNumber, array, [j, j + 1], 4, "swap", BuildBubblePartition(i, i + 1, array.Length));
                }
            }

            if (!swapped)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    array,
                    [],
                    5,
                    "early_exit",
                    BuildBubblePartition(i, array.Length, array.Length));
                break;
            }
        }

        AddStep(
            steps,
            ref stepNumber,
            array,
            [],
            6,
            "complete",
            BuildBubblePartition(array.Length, array.Length, array.Length));

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
        string actionLabel,
        BubbleStepModel? bubble)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            Bubble = bubble
        });
    }

    private static BubbleStepModel BuildBubblePartition(int passIndex, int passNumber, int length)
    {
        if (length <= 0)
        {
            return new BubbleStepModel
            {
                PassNumber = passNumber,
                SortedStartIndex = -1,
                SortedEndIndex = -1,
                UnsortedStartIndex = -1,
                UnsortedEndIndex = -1,
                Phase = "complete"
            };
        }

        var sortedCount = Math.Clamp(passIndex, 0, length);
        var sortedStart = length - sortedCount;
        var unsortedEnd = sortedStart - 1;

        var phase = sortedCount >= length
            ? "complete"
            : sortedCount == 0
                ? "initial"
                : "partitioned";

        return new BubbleStepModel
        {
            PassNumber = passNumber,
            SortedStartIndex = sortedCount == 0 ? -1 : sortedStart,
            SortedEndIndex = sortedCount == 0 ? -1 : length - 1,
            UnsortedStartIndex = sortedCount >= length ? -1 : 0,
            UnsortedEndIndex = sortedCount >= length ? -1 : unsortedEnd,
            Phase = phase
        };
    }
}
