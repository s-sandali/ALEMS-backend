using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class BubbleSortSimulationEngineTests
{
    private readonly BubbleSortSimulationEngine _sut = new();

    [Fact]
    public void Run_WhenArrayIsAlreadySorted_UsesEarlyExitTraceThatMatchesPseudocode()
    {
        var response = _sut.Run([1, 2, 3]);

        response.Steps.Should().ContainSingle(step => step.LineNumber == 5 && step.ActionLabel == "early_exit");
        response.Steps.Should().NotContain(step => step.ActionLabel == "sorted");
        response.Steps.Last().LineNumber.Should().Be(6);
        response.Steps.Last().ActionLabel.Should().Be("complete");
    }

    [Fact]
    public void Run_UT_SYNC_01_AllStepsHaveNonZeroLineNumber()
    {
        var response = _sut.Run([5, 1, 4, 2, 8]);

        response.Steps.Should().NotBeEmpty();
        response.Steps.Should().OnlyContain(step => step.LineNumber > 0);
    }

    [Fact]
    public void Run_UT_SYNC_02_AllStepLineNumbersStayWithinBubbleSortPseudocodeRange()
    {
        var response = _sut.Run([5, 1, 4, 2, 8]);

        response.Steps.Should().NotBeEmpty();
        response.Steps.Should().OnlyContain(step => step.LineNumber >= 1 && step.LineNumber <= 6);
    }
}
