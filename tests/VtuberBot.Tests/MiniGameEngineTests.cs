using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class MiniGameEngineTests
{
    [Theory]
    [InlineData("Cara", 0, true, 2)]
    [InlineData("Cara", 1, false, 0)]
    [InlineData("Cruz", 0, false, 0)]
    [InlineData("Cruz", 1, true, 2)]
    public void CoinUsesChosenFaceAndPaysOnlyWhenCorrect(string choice, int roll, bool expectedWin, int multiplier)
    {
        var result = MiniGameEngine.Resolve("Duelo de moneda", choice, roll);
        Assert.Equal(expectedWin, result.Won);
        Assert.Equal(multiplier, result.PayoutMultiplier);
        Assert.Equal(roll == 0 ? "Cara" : "Cruz", result.Outcome);
    }

    [Theory]
    [InlineData("Rojo", 1, true, 2)]
    [InlineData("Negro", 2, true, 2)]
    [InlineData("Rojo", 2, false, 0)]
    [InlineData("Verde", 0, true, 36)]
    [InlineData("Negro", 0, false, 0)]
    public void RouletteUsesEuropeanWheelColors(string choice, int roll, bool expectedWin, int multiplier)
    {
        var result = MiniGameEngine.Resolve("Ruleta de suerte", choice, roll);
        Assert.Equal(expectedWin, result.Won);
        Assert.Equal(multiplier, result.PayoutMultiplier);
        Assert.Contains(roll.ToString(), result.Outcome);
    }

    [Theory]
    [InlineData("Bajo (1-3)", 1, true)]
    [InlineData("Bajo (1-3)", 3, true)]
    [InlineData("Bajo (1-3)", 4, false)]
    [InlineData("Alto (4-6)", 4, true)]
    [InlineData("Alto (4-6)", 6, true)]
    [InlineData("Alto (4-6)", 3, false)]
    public void DiceUsesTheSelectedHalf(string choice, int roll, bool expectedWin)
    {
        var result = MiniGameEngine.Resolve("Dado", choice, roll);
        Assert.Equal(expectedWin, result.Won);
        Assert.Equal(expectedWin ? 2 : 0, result.PayoutMultiplier);
        Assert.Contains(roll.ToString(), result.Outcome);
    }

    [Fact]
    public void RejectsUnknownGamesChoicesAndOutOfRangeRolls()
    {
        Assert.Throws<ArgumentException>(() => MiniGameEngine.GetChoices("Slot machine"));
        Assert.Throws<ArgumentException>(() => MiniGameEngine.Resolve("Dado", "Rojo", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MiniGameEngine.Resolve("Duelo de moneda", "Cara", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => MiniGameEngine.Resolve("Ruleta de suerte", "Verde", 37));
        Assert.Throws<ArgumentOutOfRangeException>(() => MiniGameEngine.Resolve("Dado", "Alto (4-6)", 0));
    }
}
