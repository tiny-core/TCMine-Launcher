using System.IO;
using System.Linq;
using TCMine_Launcher.Models;
using Xunit;

namespace TCMine_Launcher.Tests;

public class PlayerDataProfileTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tcmine-test-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void EnumerateExisting_EmptyOrMissingDir_ReturnsNothing()
    {
        Assert.Empty(PlayerDataProfile.EnumerateExisting(NewTempDir()));
        Assert.Empty(PlayerDataProfile.EnumerateExisting(Path.Combine(Path.GetTempPath(), "nope-" + Path.GetRandomFileName())));
    }

    [Fact]
    public void EnumerateExisting_PicksExactFiles_NormalizedWithForwardSlash()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "options.txt"), "x");
        File.WriteAllText(Path.Combine(dir, "optionsshaders.txt"), "y");
        File.WriteAllText(Path.Combine(dir, "unrelated.dat"), "z"); // não é player-owned

        var found = PlayerDataProfile.EnumerateExisting(dir);

        Assert.Contains("options.txt", found);
        Assert.Contains("optionsshaders.txt", found);
        Assert.DoesNotContain("unrelated.dat", found);
    }

    [Fact]
    public void EnumerateExisting_ExpandsDirectoriesRecursively()
    {
        var dir = NewTempDir();
        var waypoints = Path.Combine(dir, "XaeroWaypoints", "world");
        Directory.CreateDirectory(waypoints);
        File.WriteAllText(Path.Combine(waypoints, "wp.txt"), "x");

        var found = PlayerDataProfile.EnumerateExisting(dir);

        Assert.Contains("XaeroWaypoints/world/wp.txt", found);
        Assert.All(found, p => Assert.DoesNotContain('\\', p)); // sempre '/'
    }

    [Fact]
    public void EnumerateExisting_ResolvesGlobs()
    {
        var dir = NewTempDir();
        var shaderpacks = Path.Combine(dir, "shaderpacks");
        Directory.CreateDirectory(shaderpacks);
        File.WriteAllText(Path.Combine(shaderpacks, "MyShader.txt"), "x"); // casa shaderpacks/*.txt
        File.WriteAllText(Path.Combine(shaderpacks, "MyShader.zip"), "x"); // não casa

        var found = PlayerDataProfile.EnumerateExisting(dir);

        Assert.Contains("shaderpacks/MyShader.txt", found);
        Assert.DoesNotContain(found, p => p.EndsWith(".zip"));
    }
}
