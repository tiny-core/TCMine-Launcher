using TCMine_Launcher.Models;
using Xunit;

namespace TCMine_Launcher.Tests;

public class PlayerProfileTests
{
    [Theory]
    [InlineData("Steve", "ST")]
    [InlineData("a", "A")]
    [InlineData("Jocian", "JO")]
    public void ComputeInitials_UpToTwoUppercase(string name, string expected)
    {
        Assert.Equal(expected, new PlayerProfile { Name = name }.ComputeInitials());
    }

    [Fact]
    public void HeadUrl_NullWithoutUuid_SetWithUuid()
    {
        Assert.Null(new PlayerProfile { Uuid = "" }.HeadUrl);
        Assert.Contains("abc123", new PlayerProfile { Uuid = "abc123" }.HeadUrl);
    }

    [Fact]
    public void AccountLabel_ReflectsType()
    {
        Assert.Equal("Conta Microsoft", new PlayerProfile { AccountType = AccountType.Microsoft }.AccountLabel);
        Assert.Contains("Offline", new PlayerProfile { AccountType = AccountType.Offline }.AccountLabel);
    }
}

public class GameProfileTests
{
    [Fact]
    public void JvmMemoryArgs_UsesAllocatedRam()
    {
        Assert.Equal("-Xms512m -Xmx8192m", new GameProfile { AllocatedRamMb = 8192 }.JvmMemoryArgs);
    }
}

public class MinecraftInstanceTests
{
    [Fact]
    public void VersionSummary_WithoutManifest_ShowsMcAndNeoForge()
    {
        var i = new MinecraftInstance { MinecraftVersion = "1.21.1", NeoForgeVersion = "21.1.172" };
        Assert.Equal("MC 1.21.1 · NeoForge 21.1.172", i.VersionSummary);
    }

    [Fact]
    public void VersionSummary_WithManifest_PrefixesModpackVersion()
    {
        var i = new MinecraftInstance { ManifestVersion = "1.2", MinecraftVersion = "1.21.1", NeoForgeVersion = "21.1.172" };
        Assert.StartsWith("v1.2 ·", i.VersionSummary);
    }

    [Fact]
    public void IsOfficial_AndSourceLabel_FollowSource()
    {
        Assert.True(new MinecraftInstance { Source = InstanceSource.OfficialManifest }.IsOfficial);
        Assert.Equal("OFICIAL", new MinecraftInstance { Source = InstanceSource.OfficialManifest }.SourceLabel);
        Assert.False(new MinecraftInstance { Source = InstanceSource.Manual }.IsOfficial);
        Assert.Equal("PERSONALIZADA", new MinecraftInstance { Source = InstanceSource.Manual }.SourceLabel);
    }

    [Fact]
    public void ModsLabel_HandlesEmptyAndCount()
    {
        Assert.Equal("Sem mods", new MinecraftInstance().ModsLabel);
        var i = new MinecraftInstance();
        i.Mods.Add(new ModEntry());
        Assert.Equal("1 mod(s)", i.ModsLabel);
    }

    [Fact]
    public void Initial_UppercasesFirstLetter()
    {
        Assert.Equal("T", new MinecraftInstance { Name = "tcmine" }.Initial);
        Assert.Equal("?", new MinecraftInstance { Name = "  " }.Initial);
    }
}
