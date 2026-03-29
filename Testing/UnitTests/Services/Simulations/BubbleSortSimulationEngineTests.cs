using backend.Services.Simulations;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services.Simulations;

public class BubbleSortSimulationEngineTests
{
    private readonly BubbleSortSimulationEngine _sut = new();

    [Fact(DisplayName = "UT-BUB-01 - BubbleSortEngine: start step has initial partition metadata")]
    public void Run_StartStep_HasInitialPartitionMetadata()
    {
        var response = _sut.Run([5, 1, 4, 2]);

        var start = response.Steps.First();
        start.ActionLabel.Should().Be("start");
        start.Bubble.Should().NotBeNull();
        start.Bubble!.PassNumber.Should().Be(0);
        start.Bubble.Phase.Should().Be("initial");
        start.Bubble.SortedStartIndex.Should().Be(-1);
        start.Bubble.SortedEndIndex.Should().Be(-1);
        start.Bubble.UnsortedStartIndex.Should().Be(0);
        start.Bubble.UnsortedEndIndex.Should().Be(3);
    }

    [Fact(DisplayName = "UT-BUB-02 - BubbleSortEngine: pass steps track shrinking unsorted partition")]
    public void Run_PassSteps_TrackShrinkingUnsortedPartition()
    {
        var response = _sut.Run([4, 3, 2, 1]);

        var firstPass = response.Steps.First(step => step.ActionLabel == "pass_start");
        var secondPass = response.Steps.Where(step => step.ActionLabel == "pass_start").Skip(1).First();

        firstPass.Bubble.Should().NotBeNull();
        firstPass.Bubble!.PassNumber.Should().Be(1);
        firstPass.Bubble.SortedStartIndex.Should().Be(-1);
        firstPass.Bubble.SortedEndIndex.Should().Be(-1);
        firstPass.Bubble.UnsortedStartIndex.Should().Be(0);
        firstPass.Bubble.UnsortedEndIndex.Should().Be(3);

        secondPass.Bubble.Should().NotBeNull();
        secondPass.Bubble!.PassNumber.Should().Be(2);
        secondPass.Bubble.SortedStartIndex.Should().Be(3);
        secondPass.Bubble.SortedEndIndex.Should().Be(3);
        secondPass.Bubble.UnsortedStartIndex.Should().Be(0);
        secondPass.Bubble.UnsortedEndIndex.Should().Be(2);
    }

    [Fact(DisplayName = "UT-BUB-03 - BubbleSortEngine: complete step marks partitions as fully sorted")]
    public void Run_CompleteStep_MarksFullySortedPartition()
    {
        var response = _sut.Run([3, 1, 2]);

        var complete = response.Steps.Last(step => step.ActionLabel == "complete");
        complete.Bubble.Should().NotBeNull();
        complete.Bubble!.Phase.Should().Be("complete");
        complete.Bubble.UnsortedStartIndex.Should().Be(-1);
        complete.Bubble.UnsortedEndIndex.Should().Be(-1);
        complete.Bubble.SortedStartIndex.Should().Be(0);
        complete.Bubble.SortedEndIndex.Should().Be(2);
    }
}
