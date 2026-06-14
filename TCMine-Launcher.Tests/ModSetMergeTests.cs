using System.Linq;
using TCMine.Core;
using Xunit;

namespace TCMine_Launcher.Tests;

public class ModSetMergeTests
{
    private record Mod(long Id, string Version);

    [Fact]
    public void Merge_AddsNew_KeepsExisting()
    {
        var current = new[] { new Mod(1, "a"), new Mod(2, "a") };
        var incoming = new[] { new Mod(3, "a") };

        var r = ModSetMerge.Merge(current, incoming, m => m.Id);

        Assert.Equal(1, r.Added);
        Assert.Equal(0, r.Updated);
        Assert.Equal(new long[] { 1, 2, 3 }, r.Items.Select(m => m.Id));
    }

    [Fact]
    public void Merge_UpdatesExisting_InPlace()
    {
        var current = new[] { new Mod(1, "v1"), new Mod(2, "v1") };
        var incoming = new[] { new Mod(2, "v2") };

        var r = ModSetMerge.Merge(current, incoming, m => m.Id);

        Assert.Equal(0, r.Added);
        Assert.Equal(1, r.Updated);
        Assert.Equal("v2", r.Items.Single(m => m.Id == 2).Version); // substituído
        Assert.Equal("v1", r.Items.Single(m => m.Id == 1).Version); // mantido
        Assert.Equal(new long[] { 1, 2 }, r.Items.Select(m => m.Id)); // ordem preservada
    }

    [Fact]
    public void Merge_MixedAddAndUpdate()
    {
        var current = new[] { new Mod(1, "v1"), new Mod(2, "v1") };
        var incoming = new[] { new Mod(2, "v2"), new Mod(3, "v1") };

        var r = ModSetMerge.Merge(current, incoming, m => m.Id);

        Assert.Equal(1, r.Added);
        Assert.Equal(1, r.Updated);
        Assert.Equal(3, r.Items.Count);
    }

    [Fact]
    public void Merge_EmptyIncoming_KeepsAll()
    {
        var current = new[] { new Mod(1, "a"), new Mod(2, "a") };

        var r = ModSetMerge.Merge(current, System.Array.Empty<Mod>(), m => m.Id);

        Assert.Equal(0, r.Added);
        Assert.Equal(0, r.Updated);
        Assert.Equal(2, r.Items.Count);
    }
}
