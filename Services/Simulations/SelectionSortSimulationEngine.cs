using backend.Models.Simulations;

namespace backend.Services.Simulations;

/// <summary>
/// Generates step-by-step simulation output for Selection Sort.
/// </summary>
public class SelectionSortSimulationEngine : IAlgorithmSimulationEngine
{
    private const string AlgorithmDisplayName = "Selection Sort";

    private static class PseudocodeLine
    {
        public const int Start = 1;
        public const int PassStart = 2;
        public const int Compare = 3;
        public const int SelectMin = 4;
        public const int Swap = 5;
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
        return normalized is "selection_sort" or "selection-sort";
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
            new SelectionSortStepModel
            {
                Type = "start"
            });

        for (var i = 0; i < values.Length - 1; i++)
        {
            var minIndex = i;

            AddStep(
                steps,
                ref stepNumber,
                values,
                [i],
                PseudocodeLine.PassStart,
                "pass_start",
                new SelectionSortStepModel
                {
                    Type = "pass_start",
                    CurrentIndex = i,
                    MinIndex = minIndex
                });

            for (var j = i + 1; j < values.Length; j++)
            {
                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [minIndex, j],
                    PseudocodeLine.Compare,
                    "compare",
                    new SelectionSortStepModel
                    {
                        Type = "compare",
                        CurrentIndex = i,
                        CandidateIndex = j,
                        MinIndex = minIndex
                    });

                if (values[j] >= values[minIndex])
                {
                    continue;
                }

                minIndex = j;

                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [minIndex],
                    PseudocodeLine.SelectMin,
                    "select_min",
                    new SelectionSortStepModel
                    {
                        Type = "select_min",
                        CurrentIndex = i,
                        CandidateIndex = j,
                        MinIndex = minIndex
                    });
            }

            if (minIndex != i)
            {
                (values[i], values[minIndex]) = (values[minIndex], values[i]);

                AddStep(
                    steps,
                    ref stepNumber,
                    values,
                    [i, minIndex],
                    PseudocodeLine.Swap,
                    "swap",
                    new SelectionSortStepModel
                    {
                        Type = "swap",
                        CurrentIndex = i,
                        MinIndex = minIndex,
                        SwapFrom = i,
                        SwapTo = minIndex
                    });
            }

            AddStep(
                steps,
                ref stepNumber,
                values,
                BuildSortedBoundaryIndices(i),
                PseudocodeLine.SortedBoundary,
                "sorted_boundary",
                new SelectionSortStepModel
                {
                    Type = "sorted_boundary",
                    CurrentIndex = i,
                    SortedBoundary = i,
                    MinIndex = i
                });
        }

        if (values.Length > 0)
        {
            var finalBoundary = values.Length - 1;
            AddStep(
                steps,
                ref stepNumber,
                values,
                BuildSortedBoundaryIndices(finalBoundary),
                PseudocodeLine.SortedBoundary,
                "sorted_boundary",
                new SelectionSortStepModel
                {
                    Type = "sorted_boundary",
                    CurrentIndex = finalBoundary,
                    SortedBoundary = finalBoundary,
                    MinIndex = finalBoundary
                });
        }

        AddStep(
            steps,
            ref stepNumber,
            values,
            [],
            PseudocodeLine.Complete,
            "complete",
            new SelectionSortStepModel
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
        SelectionSortStepModel? selectionSort)
    {
        steps.Add(new SimulationStep
        {
            StepNumber = stepNumber++,
            ArrayState = array.ToArray(),
            ActiveIndices = activeIndices,
            LineNumber = lineNumber,
            ActionLabel = actionLabel,
            SelectionSort = selectionSort
        });
    }
}
