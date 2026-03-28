using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class SimulationSynchronizationTests
{
    [Fact(DisplayName = "UT-SYNC-01 — SimulationStepModel: line_number is present and non-zero on every step")]
    public void StepGenerators_ShouldAlwaysPopulateNonZeroLineNumber()
    {
        var bubble = new BubbleSortSimulationEngine();
        var binary = new BinarySearchSimulationEngine();

        var bubbleSteps = bubble.Run([5, 1, 4, 2]).Steps;
        var binaryFoundSteps = binary.Run([3, 7, 12, 19], targetValue: 12).Steps;
        var binaryNotFoundSteps = binary.Run([3, 7, 12, 19], targetValue: 100).Steps;
        var binaryEmptySteps = binary.Run([], targetValue: 1).Steps;

        bubbleSteps.Should().NotBeEmpty();
        binaryFoundSteps.Should().NotBeEmpty();
        binaryNotFoundSteps.Should().NotBeEmpty();
        binaryEmptySteps.Should().NotBeEmpty();

        bubbleSteps.Should().OnlyContain(step => step.LineNumber > 0);
        binaryFoundSteps.Should().OnlyContain(step => step.LineNumber > 0);
        binaryNotFoundSteps.Should().OnlyContain(step => step.LineNumber > 0);
        binaryEmptySteps.Should().OnlyContain(step => step.LineNumber > 0);
    }

    [Fact(DisplayName = "UT-SYNC-02 — SimulationStepModel: line_number values fall within valid pseudocode ranges")]
    public void StepGenerators_ShouldEmitLineNumbersWithinAlgorithmPseudocodeRange()
    {
        var bubble = new BubbleSortSimulationEngine();
        var binary = new BinarySearchSimulationEngine();

        var bubbleCases = new[]
        {
            bubble.Run([1, 2, 3, 4]).Steps,
            bubble.Run([4, 3, 2, 1]).Steps,
            bubble.Run([42]).Steps,
            bubble.Run([]).Steps
        };

        var binaryCases = new[]
        {
            binary.Run([3, 7, 12, 19], targetValue: 12).Steps,
            binary.Run([3, 7, 12, 19], targetValue: -1).Steps,
            binary.Run([], targetValue: 99).Steps
        };

        foreach (var steps in bubbleCases)
        {
            steps.Should().OnlyContain(step => step.LineNumber >= 1 && step.LineNumber <= 6);
        }

        foreach (var steps in binaryCases)
        {
            steps.Should().OnlyContain(step => step.LineNumber >= 1 && step.LineNumber <= 8);
        }
    }
}
