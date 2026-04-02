using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Insertion Sort.
/// </summary>
public class InsertionSortSimulationEngine : IAlgorithmSimulationEngine
{
    private const string AlgorithmDisplayName = "Insertion Sort";

    private static class PseudocodeLine
    {
        public const int Start = 1;
        public const int SelectKey = 2;
        public const int Compare = 3;
        public const int Shift = 4;
        public const int Insert = 5;
        public const int SortedBoundary = 6;
        public const int Complete = 7;
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

        AddStep(
            steps,
            ref stepNumber,
            values,
            [],
            PseudocodeLine.Start,
            "start",
            new InsertionSortStepModel
            {
                Type = "start"
            });

        for (var i = 1; i < values.Length; i++)
        {
            var key = values[i];
            var j = i - 1;

            AddStep(
                steps,
                ref stepNumber,
                values,
                [i],
                PseudocodeLine.SelectKey,
                "select_key",
                new InsertionSortStepModel
                {
                    Type = "select_key",
                    CurrentIndex = i,
                    Key = key
                });

            while (j >= 0)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [j, j + 1],
                    PseudocodeLine.Compare,
                    "compare",
                    new InsertionSortStepModel
                    {
                        Type = "compare",
                        CurrentIndex = i,
                        Key = key,
                        CompareIndex = j
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
                    PseudocodeLine.Shift,
                    "shift",
                    new InsertionSortStepModel
                    {
                        Type = "shift",
                        CurrentIndex = i,
                        Key = key,
                        CompareIndex = j,
                        ShiftFrom = j,
                        ShiftTo = j + 1
                    });

                j--;
            }

            values[j + 1] = key;
            AddStep(
                steps,
                ref stepNumber,
                values,
                [j + 1],
                PseudocodeLine.Insert,
                "insert",
                new InsertionSortStepModel
                {
                    Type = "insert",
                    CurrentIndex = i,
                    Key = key,
                    InsertPosition = j + 1
                });

            AddStep(
                steps,
                ref stepNumber,
                values,
                BuildSortedBoundaryIndices(i),
                PseudocodeLine.SortedBoundary,
                "sorted_boundary",
                new InsertionSortStepModel
                {
                    Type = "sorted_boundary",
                    CurrentIndex = i,
                    Key = key,
                    SortedBoundary = i
                });
        }

        AddStep(
            steps,
            ref stepNumber,
            values,
            [],
            PseudocodeLine.Complete,
            "complete",
            new InsertionSortStepModel
            {
                Type = "complete"
            });

        return new SimulationResponse
        {
            AlgorithmName = AlgorithmDisplayName,
            Steps = steps,
            TotalSteps = steps.Count
        };
    }

    private static int[] BuildSortedBoundaryIndices(int boundary)
    {
        var indices = new int[boundary + 1];
        for (var i = 0; i <= boundary; i++)
        {
            indices[i] = i;
        }

        return indices;
    }

    private static void AddStep(
        List<SimulationStep> steps,
        ref int stepNumber,
        int[] array,
        int[] activeIndices,
        int lineNumber,
        string actionLabel,
        InsertionSortStepModel? insertionSort)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            InsertionSort = insertionSort
        });
    }
}