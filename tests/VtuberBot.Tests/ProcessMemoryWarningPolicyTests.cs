using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ProcessMemoryWarningPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1024)]
    [InlineData(838860799)]
    public void EvaluateBelow800MiBHasNoWarning(long bytes) =>
        Assert.Equal(ProcessMemoryWarningLevel.None, ProcessMemoryWarningPolicy.Evaluate(bytes));

    [Theory]
    [InlineData(838860800)]
    [InlineData(1073741823)]
    public void EvaluateFrom800MiBToBelow1GiBWarnsEarly(long bytes) =>
        Assert.Equal(ProcessMemoryWarningLevel.ApproachingLimit, ProcessMemoryWarningPolicy.Evaluate(bytes));

    [Theory]
    [InlineData(1073741824)]
    [InlineData(2147483648)]
    public void EvaluateAtOrAbove1GiBRaisesLimitWarning(long bytes) =>
        Assert.Equal(ProcessMemoryWarningLevel.LimitReached, ProcessMemoryWarningPolicy.Evaluate(bytes));

    [Fact]
    public void EvaluateUnavailableMeasurementDoesNotInventMemoryUsage()
    {
        Assert.Equal(ProcessMemoryWarningLevel.None, ProcessMemoryWarningPolicy.Evaluate(null));
        Assert.Equal(ProcessMemoryWarningLevel.None, ProcessMemoryWarningPolicy.Evaluate(-1));
    }
}
