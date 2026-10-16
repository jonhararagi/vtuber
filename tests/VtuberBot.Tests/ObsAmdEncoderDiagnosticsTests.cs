using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsAmdEncoderDiagnosticsTests
{
    [Theory]
    [InlineData(false, false, false, ObsAmdEncoderEvidenceLevel.NoAmdAdapterDetected)]
    [InlineData(true, false, false, ObsAmdEncoderEvidenceLevel.AmdAdapterOnly)]
    [InlineData(true, true, false, ObsAmdEncoderEvidenceLevel.AmfPluginAndAmdAdapter)]
    [InlineData(false, false, true, ObsAmdEncoderEvidenceLevel.AmfMentionedInRecentLogs)]
    public void ClassifiesEvidenceWithoutClaimingEncoderIsVerified(
        bool amdAdapterDetected,
        bool amfPluginFound,
        bool amfLogReference,
        ObsAmdEncoderEvidenceLevel expected) =>
        Assert.Equal(expected, ObsAmdEncoderDiagnostic.AssessEvidence(
            amdAdapterDetected, amfPluginFound, amfLogReference));

    [Fact]
    public void CandidateFileAndGpuAreOnlyEvidenceNotConfirmation()
    {
        var report = new ObsAmdEncoderDiagnostic(
            new[] { "AMD Radeon Graphics" },
            @"C:\OBS\obs-plugins\64bit\obs-amd-encoder.dll",
            false,
            ObsAmdEncoderEvidenceLevel.AmfPluginAndAmdAdapter);

        Assert.Contains("aún no está confirmada", report.Summary);
        Assert.Contains("no publica un inventario", report.Details);
    }
}
