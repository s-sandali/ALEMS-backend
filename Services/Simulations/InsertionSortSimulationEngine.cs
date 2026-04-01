using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Insertion Sort.
/// </summary>
public class InsertionSortSimulationEngine : IAlgorithmSimulationEngine
{
    private sealed class InsertionStepMeta
    {
        public int? KeyIndex { get; init; }
        public int? Key { get; init; }
        public int? CompareIndex { get; init; }
        public int? SortedBoundary { get; init; }
    }

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
        var values = array.ToArray();
        var steps = new List<SimulationStep>();
        var stepNumber = 1;

        AddStep(steps, ref stepNumber, values, [], 1, "start");

        for (var i = 1; i < values.Length; i++)
        {
            var key = values[i];
            var j = i - 1;

            AddStep(
                steps,
                ref stepNumber,
                values,
                [i],
                2,
                "select_key",
                new InsertionStepMeta
                {
                    KeyIndex = i,
                    Key = key,
                    SortedBoundary = i - 1
                });

            while (j >= 0)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [j, j + 1],
                    3,
                    "compare",
                    new InsertionStepMeta
                    {
                        KeyIndex = i,
                        Key = key,
                        CompareIndex = j,
                        SortedBoundary = i - 1
                    });

                if (values[j] <= key)
                {
                    break;
                }

                values[j + 1] = values[j];
                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [j, j + 1],
                    4,
                    "shift",
                    new InsertionStepMeta
                    {
                        KeyIndex = i,
                        Key = key,
                        CompareIndex = j,
                        SortedBoundary = i - 1
                    });

                j--;
            }

            values[j + 1] = key;
            AddStep(
                steps,
                ref stepNumber,
                values,
                [j + 1],
                5,
                "insert",
                new InsertionStepMeta
                {
                    KeyIndex = i,
                    Key = key,
                    CompareIndex = j,
                    SortedBoundary = i
                });
        }

        AddStep(steps, ref stepNumber, values, [], 6, "complete");

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
        string actionLabel,
        InsertionStepMeta? meta = null)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            KeyIndex = meta?.KeyIndex,
            Key = meta?.Key,
            CompareIndex = meta?.CompareIndex,
            SortedBoundary = meta?.SortedBoundary
        });
    }
}
