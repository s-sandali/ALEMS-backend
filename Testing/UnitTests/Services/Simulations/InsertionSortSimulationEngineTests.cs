using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class InsertionSortSimulationEngineTests
{
    private readonly InsertionSortSimulationEngine _sut = new();

    [Theory]
    [InlineData("insertion_sort")]
    [InlineData("insertion-sort")]
    [InlineData("  InSeRtIoN_SoRt  ")]
    public void CanHandle_RecognizedInsertionSortKeys_ReturnsTrue(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("bubble_sort")]
    public void CanHandle_UnknownOrEmptyKeys_ReturnsFalse(string algorithm)
    {
        _sut.CanHandle(algorithm).Should().BeFalse();
    }

    [Fact(DisplayName = "UT-IS-01 - InsertionSortEngine: emits compare, shift, and insert actions")]
    public void Run_EmitsInsertionSortActionLabels()
    {
        var response = _sut.Run([5, 2, 4, 6, 1, 3]);
        var labels = response.Steps.Select(step => step.ActionLabel).ToArray();

        labels.Should().Contain("compare");
        labels.Should().Contain("shift");
        labels.Should().Contain("insert");
        labels.Should().Contain("complete");
    }

    [Fact(DisplayName = "UT-IS-02 - InsertionSortEngine: final array state is sorted ascending")]
    public void Run_FinalArray_IsSortedAscending()
    {
        var response = _sut.Run([5, 2, 4, 6, 1, 3]);

        response.AlgorithmName.Should().Be("Insertion Sort");
        response.Steps[response.Steps.Count - 1].ArrayState.Should().Equal(1, 2, 3, 4, 5, 6);
    }
}