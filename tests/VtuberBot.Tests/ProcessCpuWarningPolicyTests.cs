using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ProcessCpuWarningPolicyTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(35d)]
    [InlineData(79.99d)]
    public void EvaluateBelow80PercentIsNormal(double value) =>
        Assert.Equal(ProcessCpuWarningLevel.Normal, ProcessCpuWarningPolicy.Evaluate(value));

    [Theory]
    [InlineData(80d)]
    [InlineData(94.99d)]
    public void EvaluateFrom80ToBelow95PercentWarns(double value) =>
        Assert.Equal(ProcessCpuWarningLevel.HighLoad, ProcessCpuWarningPolicy.Evaluate(value));

    [Theory]
    [InlineData(95d)]
    [InlineData(100d)]
    [InlineData(150d)]
    public void EvaluateAtOrAbove95PercentRaisesCriticalWarning(double value) =>
        Assert.Equal(ProcessCpuWarningLevel.Critical, ProcessCpuWarningPolicy.Evaluate(value));

    [Fact]
    public void EvaluateInvalidMeasurementDoesNotInventCpuLoad()
    {
        Assert.Equal(ProcessCpuWarningLevel.Unavailable, ProcessCpuWarningPolicy.Evaluate(null));
        Assert.Equal(ProcessCpuWarningLevel.Unavailable, ProcessCpuWarningPolicy.Evaluate(-1d));
        Assert.Equal(ProcessCpuWarningLevel.Unavailable, ProcessCpuWarningPolicy.Evaluate(double.NaN));
        Assert.Equal(ProcessCpuWarningLevel.Unavailable, ProcessCpuWarningPolicy.Evaluate(double.PositiveInfinity));
    }
}
