using backend.Services;
using FluentAssertions;
using Xunit;

namespace backend.Tests.Services;

public class XpServiceTests
{
    private readonly XpService _sut = new();

    [Theory(DisplayName = "CalculateXP returns expected rewards for supported activity types and difficulties")]
    [InlineData("quiz", "easy", 10)]
    [InlineData("QUIZ", "MEDIUM", 20)]
    [InlineData("coding", "hard", 200)]
    [InlineData("CoDiNg", "Easy", 50)]
    public void CalculateXP_SupportedInputs_ReturnsExpectedReward(string type, string difficulty, int expectedXp)
    {
        var result = _sut.CalculateXP(type, difficulty);

        result.Should().Be(expectedXp);
    }

    [Fact(DisplayName = "CalculateXP throws for unsupported difficulty")]
    public void CalculateXP_UnsupportedDifficulty_ThrowsArgumentException()
    {
        var action = () => _sut.CalculateXP("quiz", "expert");

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("difficulty")
            .WithMessage("*expert*");
    }

    [Fact(DisplayName = "CalculateXP throws for unsupported activity type")]
    public void CalculateXP_UnsupportedType_ThrowsArgumentException()
    {
        var action = () => _sut.CalculateXP("simulation", "easy");

        action.Should()
            .Throw<ArgumentException>()
            .WithParameterName("type")
            .WithMessage("*simulation*");
    }
}
