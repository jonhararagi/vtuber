using VtuberBot.App;
using Xunit;

namespace VtuberBot.Tests;

public sealed class UserSecretStoreTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BlankOptionalSecretPreservesPreviouslySavedValue(string? optionalValue)
    {
        var directory = Path.Combine(Path.GetTempPath(), "VtuberBot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSecretStore(directory);
            store.Save("kick-client-secret", "existing-secret-value");

            var saved = store.SaveIfProvided("kick-client-secret", optionalValue);

            Assert.False(saved);
            Assert.Equal("existing-secret-value", store.Read("kick-client-secret"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void NonBlankOptionalSecretReplacesPreviouslySavedValue()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VtuberBot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new UserSecretStore(directory);
            store.Save("kick-client-secret", "old-secret");

            var saved = store.SaveIfProvided("kick-client-secret", "new-secret");

            Assert.True(saved);
            Assert.Equal("new-secret", store.Read("kick-client-secret"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
