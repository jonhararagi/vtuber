using Xunit;

namespace VtuberBot.App.Tests;

public sealed class ObsSetupStepEvidencePolicyTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void ManualConfirmationCannotCompleteAnAppVerifiedStep(int stepIndex)
    {
        Assert.True(ObsSetupStepEvidencePolicy.RequiresApplicationVerification(stepIndex));
        Assert.Equal(
            ObsSetupStepEvidenceState.Pending,
            ObsSetupStepEvidencePolicy.Resolve(stepIndex, manuallyConfirmed: true, applicationVerified: false));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void SuccessfulAppCheckIsRecordedAsAppVerified(int stepIndex)
    {
        Assert.Equal(
            ObsSetupStepEvidenceState.VerifiedByApplication,
            ObsSetupStepEvidencePolicy.Resolve(stepIndex, manuallyConfirmed: false, applicationVerified: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void OtherStepsCanOnlyBeMarkedAsManuallyConfirmed(int stepIndex)
    {
        Assert.False(ObsSetupStepEvidencePolicy.RequiresApplicationVerification(stepIndex));
        Assert.Equal(
            ObsSetupStepEvidenceState.ConfirmedManually,
            ObsSetupStepEvidencePolicy.Resolve(stepIndex, manuallyConfirmed: true, applicationVerified: true));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void RejectsStepIndexesOutsideTheWizard(int stepIndex)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ObsSetupStepEvidencePolicy.Resolve(stepIndex, manuallyConfirmed: true, applicationVerified: true));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ObsSetupStepEvidencePolicy.RequiresApplicationVerification(stepIndex));
    }
}
