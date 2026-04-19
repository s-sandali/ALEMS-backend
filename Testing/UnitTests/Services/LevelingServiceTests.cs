using backend.Services;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services;

public class LevelingServiceTests
{
    private readonly LevelingService _sut = new();

    [Theory(DisplayName = "CalculateLevel returns the expected level around XP thresholds")]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(381, 2)]
    [InlineData(382, 3)]
    [InlineData(900, 3)]
    [InlineData(901, 4)]
    public void CalculateLevel_ThresholdBoundaries_ReturnsExpectedLevel(int xpTotal, int expectedLevel)
    {
        var result = _sut.CalculateLevel(xpTotal);

        result.Should().Be(expectedLevel);
    }

    [Theory(DisplayName = "GetXpThresholdForLevel returns cumulative XP at the start of each level")]
    [InlineData(1, 0)]
    [InlineData(2, 100)]
    [InlineData(3, 382)]
    [InlineData(4, 901)]
    public void GetXpThresholdForLevel_ReturnsExpectedThreshold(int level, int expectedThreshold)
    {
        var result = _sut.GetXpThresholdForLevel(level);

        result.Should().Be(expectedThreshold);
    }

    [Fact(DisplayName = "GetXpThresholdForLevel returns zero for invalid levels")]
    public void GetXpThresholdForLevel_LevelBelowOne_ReturnsZero()
    {
        _sut.GetXpThresholdForLevel(0).Should().Be(0);
    }

    [Theory(DisplayName = "Previous and next level helpers reuse the same progression thresholds")]
    [InlineData(1, 0, 100)]
    [InlineData(2, 100, 382)]
    [InlineData(3, 382, 901)]
    public void PreviousAndNextLevelHelpers_ReturnExpectedThresholds(int currentLevel, int expectedPrevious, int expectedNext)
    {
        _sut.GetXpForPreviousLevel(currentLevel).Should().Be(expectedPrevious);
        _sut.GetXpForNextLevel(currentLevel).Should().Be(expectedNext);
    }
}
