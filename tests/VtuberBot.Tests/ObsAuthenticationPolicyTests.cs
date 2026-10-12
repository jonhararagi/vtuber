using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class ObsAuthenticationPolicyTests
{
    [Fact]
    public void RequiresServerSideAuthenticationChallenge()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ObsAuthenticationPolicy.ValidateHandshake(false, "password"));

        Assert.Contains("no exige autenticación", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingPasswordWhenServerRequiresAuthentication(string? password)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ObsAuthenticationPolicy.ValidateHandshake(true, password));

        Assert.Contains("requiere una contraseña", exception.Message);
    }

    [Fact]
    public void AcceptsConfiguredAuthenticationChallengeAndPassword() =>
        ObsAuthenticationPolicy.ValidateHandshake(true, "local-test-password");
}
