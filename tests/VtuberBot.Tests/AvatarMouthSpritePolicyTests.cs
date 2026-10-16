using VtuberBot.Core;
using Xunit;

namespace VtuberBot.Tests;

public sealed class AvatarMouthSpritePolicyTests
{
    [Fact]
    public void TalkingUsesOpenSpriteAndIdleUsesClosedSprite()
    {
        var folder = Directory.CreateTempSubdirectory("vtuber-mouth-policy-");
        try
        {
            var basePath = Path.Combine(folder.FullName, "base.png");
            var closedPath = Path.Combine(folder.FullName, "closed.png");
            var openPath = Path.Combine(folder.FullName, "open.png");
            File.WriteAllBytes(basePath, [1]);
            File.WriteAllBytes(closedPath, [2]);
            File.WriteAllBytes(openPath, [3]);
            var layer = new AvatarLayer(1, "Boca", basePath, 0, true, ClosedMouthPath: closedPath, OpenMouthPath: openPath);

            Assert.Equal(closedPath, AvatarMouthSpritePolicy.ResolvePath(layer, "Idle"));
            Assert.Equal(openPath, AvatarMouthSpritePolicy.ResolvePath(layer, "Talking"));
            Assert.Equal(openPath, AvatarMouthSpritePolicy.ResolvePath(layer, "talking"));
        }
        finally { Directory.Delete(folder.FullName, true); }
    }

    [Fact]
    public void MissingMouthSpritesFallBackWithoutHidingTheLayer()
    {
        var folder = Directory.CreateTempSubdirectory("vtuber-mouth-fallback-");
        try
        {
            var basePath = Path.Combine(folder.FullName, "base.png");
            var closedPath = Path.Combine(folder.FullName, "closed.png");
            var openPath = Path.Combine(folder.FullName, "open.png");
            File.WriteAllBytes(basePath, [1]);
            File.WriteAllBytes(closedPath, [2]);
            var layer = new AvatarLayer(1, "Boca", basePath, 0, true, ClosedMouthPath: closedPath, OpenMouthPath: openPath);

            Assert.Equal(closedPath, AvatarMouthSpritePolicy.ResolvePath(layer, "Talking"));
            Assert.Equal(basePath, AvatarMouthSpritePolicy.ResolvePath(layer, "Happy"));
        }
        finally { Directory.Delete(folder.FullName, true); }
    }
}
