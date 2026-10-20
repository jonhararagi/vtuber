namespace VtuberBot.App;

/// <summary>
/// Describes what evidence is available for each step in the OBS setup wizard.
/// Manual confirmation must never substitute for the two checks the application can perform.
/// </summary>
public enum ObsSetupStepEvidenceState
{
    Pending,
    ConfirmedManually,
    VerifiedByApplication
}

/// <summary>
/// Keeps wizard progress honest: OBS connection and profile application require real app feedback.
/// Step indexes match the seven ordered steps in <see cref="ObsSetupWizardWindow"/>.
/// </summary>
public static class ObsSetupStepEvidencePolicy
{
    public const int StepCount = 7;
    public const int ObsConnectionStep = 3;
    public const int VideoProfileStep = 5;

    public static bool RequiresApplicationVerification(int stepIndex)
    {
        ValidateStepIndex(stepIndex);
        return stepIndex is ObsConnectionStep or VideoProfileStep;
    }

    public static ObsSetupStepEvidenceState Resolve(
        int stepIndex,
        bool manuallyConfirmed,
        bool applicationVerified)
    {
        ValidateStepIndex(stepIndex);

        if (RequiresApplicationVerification(stepIndex))
            return applicationVerified
                ? ObsSetupStepEvidenceState.VerifiedByApplication
                : ObsSetupStepEvidenceState.Pending;

        return manuallyConfirmed
            ? ObsSetupStepEvidenceState.ConfirmedManually
            : ObsSetupStepEvidenceState.Pending;
    }

    private static void ValidateStepIndex(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= StepCount)
            throw new ArgumentOutOfRangeException(nameof(stepIndex), "El paso del asistente debe estar entre 0 y 6.");
    }
}
