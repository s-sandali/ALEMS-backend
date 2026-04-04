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
            [new BubbleSortSimulationEngine(), new InsertionSortSimulationEngine(), new SelectionSortSimulationEngine()],
            new InMemorySimulationSessionStore(),
            NullLogger<SimulationService>.Instance);

    [Fact(DisplayName = "UT-BS-08 - SimulationService: RunAsync returns Bubble Sort steps with mapped fields")]
    public async Task RunAsync_BubbleSort_ReturnsStepsWithAllFieldsPopulated()
    {
        var sut = BuildSut();

        var result = await sut.RunAsync("bubble_sort", [5, 1, 4, 2], null);

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

    [Fact(DisplayName = "UT-IS-04 - SimulationService: RunAsync returns Insertion Sort steps")]
    public async Task RunAsync_InsertionSort_ReturnsInsertionSortTrace()
    {
        var sut = BuildSut();

        var result = await sut.RunAsync("insertion_sort", [5, 3, 8, 4], null);

        result.AlgorithmName.Should().Be("Insertion Sort");
        result.TotalSteps.Should().Be(result.Steps.Count);
        result.Steps.Should().Contain(step => step.ActionLabel == "select_key");
        result.Steps.Should().Contain(step => step.ActionLabel == "compare");
        result.Steps.Should().Contain(step => step.ActionLabel == "shift");
        result.Steps.Should().Contain(step => step.ActionLabel == "insert");
        result.Steps.Should().Contain(step => step.ActionLabel == "sorted_boundary");
    }

    [Fact(DisplayName = "UT-BS-09 - SimulationService: RunAsync handles unknown algorithm ID gracefully")]
    public async Task RunAsync_UnknownAlgorithm_ThrowsNotSupportedException()
    {
        var sut = BuildSut();

        var act = () => sut.RunAsync("unknown_algorithm", [3, 2, 1], null);

        await act.Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*unknown_algorithm*");
    }

    [Fact(DisplayName = "UT-IS-03 - SimulationService: RunAsync supports insertion sort and returns shift metadata")]
    public async Task RunAsync_InsertionSort_ReturnsShiftStepWithMetadata()
    {
        var sut = BuildSut();

        var result = await sut.RunAsync("insertion_sort", [9, 5, 7, 3], null);

        result.AlgorithmName.Should().Be("Insertion Sort");
        result.TotalSteps.Should().Be(result.Steps.Count);

        var shiftStep = result.Steps.First(step => step.ActionLabel == "shift");

        shiftStep.InsertionSort.Should().NotBeNull();
        shiftStep.InsertionSort!.Type.Should().Be("shift");
        shiftStep.InsertionSort.Key.Should().HaveValue();
        shiftStep.InsertionSort.CompareIndex.Should().HaveValue();
        shiftStep.InsertionSort.ShiftFrom.Should().HaveValue();
        shiftStep.InsertionSort.ShiftTo.Should().HaveValue();
    }

    [Fact(DisplayName = "UT-SS-01 - SimulationService: RunAsync returns Selection Sort steps")]
    public async Task RunAsync_SelectionSort_ReturnsSelectionSortTrace()
    {
        var sut = BuildSut();

        var result = await sut.RunAsync("selection_sort", [64, 25, 12, 22, 11], null);

        result.AlgorithmName.Should().Be("Selection Sort");
        result.TotalSteps.Should().Be(result.Steps.Count);
        result.Steps.Should().Contain(step => step.ActionLabel == "compare");
        result.Steps.Should().Contain(step => step.ActionLabel == "select_min");
        result.Steps.Should().Contain(step => step.ActionLabel == "swap");
        result.Steps.Should().Contain(step => step.ActionLabel == "sorted_boundary");

        var swapStep = result.Steps.First(step => step.ActionLabel == "swap");
        swapStep.SelectionSort.Should().NotBeNull();
        swapStep.SelectionSort!.Type.Should().Be("swap");
        swapStep.SelectionSort.SwapFrom.Should().HaveValue();
        swapStep.SelectionSort.SwapTo.Should().HaveValue();
    }
}
