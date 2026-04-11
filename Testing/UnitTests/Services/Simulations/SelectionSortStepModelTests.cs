using backend.Models.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class SelectionSortStepModelTests
{
    [Fact(DisplayName = "UT-SSM-01 - SelectionSortStepModel: defaults initialize to empty type and null indices")]
    public void Constructor_Defaults_AreInitializedCorrectly()
    {
        // Act
        var model = new SelectionSortStepModel();

        // Assert
        model.Type.Should().Be(string.Empty);
        model.CurrentIndex.Should().BeNull();
        model.CandidateIndex.Should().BeNull();
        model.MinIndex.Should().BeNull();
        model.SwapFrom.Should().BeNull();
        model.SwapTo.Should().BeNull();
        model.SortedBoundary.Should().BeNull();
    }

    [Fact(DisplayName = "UT-SSM-02 - SelectionSortStepModel: properties support set and get roundtrip")]
    public void Properties_SetValues_RoundTripCorrectly()
    {
        // Arrange
        var model = new SelectionSortStepModel
        {
            Type = "swap",
            CurrentIndex = 1,
            CandidateIndex = 3,
            MinIndex = 3,
            SwapFrom = 1,
            SwapTo = 3,
            SortedBoundary = 2
        };

        // Assert
        model.Type.Should().Be("swap");
        model.CurrentIndex.Should().Be(1);
        model.CandidateIndex.Should().Be(3);
        model.MinIndex.Should().Be(3);
        model.SwapFrom.Should().Be(1);
        model.SwapTo.Should().Be(3);
        model.SortedBoundary.Should().Be(2);
    }
}
