using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarImageFormatPolicyTests
{
    [Theory]
    [InlineData(".png")]
    [InlineData(".PNG")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".bmp")]
    public void AcceptsPortableBuiltInFormats(string extension) =>
        Assert.True(AvatarImageFormatPolicy.IsSupportedExtension(extension));

    [Theory]
    [InlineData(".webp")]
    [InlineData(".gif")]
    [InlineData(".tiff")]
    [InlineData(".svg")]
    [InlineData(".exe")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsUndeclaredOrOptionalFormats(string? extension) =>
        Assert.False(AvatarImageFormatPolicy.IsSupportedExtension(extension));
}
