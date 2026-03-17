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
}
