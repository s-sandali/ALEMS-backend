using backend.Services;
using backend.Services.Simulations;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace backend.Tests.Services;

public class SimulationServiceRunTests
{
    private static SimulationService BuildSut() =>
        new(
            [new BubbleSortSimulationEngine()],
            new InMemorySimulationSessionStore(),
            NullLogger<SimulationService>.Instance);

    [Fact(DisplayName = "UT-BS-08 - SimulationService: RunAsync returns Bubble Sort steps with mapped fields")]
    public async Task RunAsync_BubbleSort_ReturnsStepsWithAllFieldsPopulated()
    {
        var sut = BuildSut();

        var result = await sut.RunAsync("bubble_sort", [5, 1, 4, 2]);

        result.AlgorithmName.Should().Be("Bubble Sort");
        result.TotalSteps.Should().Be(result.Steps.Count);
        result.Steps.Should().NotBeEmpty();

        result.Steps.Should().OnlyContain(step =>
            step.StepNumber > 0 &&
            step.ArrayState != null &&
            step.ActiveIndices != null &&
            step.LineNumber >= 1 && step.LineNumber <= 6 &&
            !string.IsNullOrWhiteSpace(step.ActionLabel));
    }

    [Fact(DisplayName = "UT-BS-09 - SimulationService: RunAsync handles unknown algorithm ID gracefully")]
    public async Task RunAsync_UnknownAlgorithm_ThrowsNotSupportedException()
    {
        var sut = BuildSut();

        var act = () => sut.RunAsync("unknown_algorithm", [3, 2, 1]);

        await act.Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*unknown_algorithm*");
    }
}
