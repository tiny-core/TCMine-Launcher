using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;
using Xunit;

namespace TCMine_Launcher.Tests;

public class ContentSyncServiceTests
{
    private sealed class FakeManifestSource : IManifestSource
    {
        public bool IsConfigured { get; set; } = true;
        public List<ModpackManifest> Summaries { get; } = new();
        public Dictionary<string, ModpackManifest?> Full { get; } = new();
        public int GetManifestCalls { get; private set; }

        public Task<List<ModpackManifest>> GetModpacksAsync(CancellationToken ct = default) =>
            Task.FromResult(Summaries);

        public Task<ModpackManifest?> GetManifestAsync(string id, CancellationToken ct = default)
        {
            GetManifestCalls++;
            return Task.FromResult(Full.TryGetValue(id, out var m) ? m : null);
        }
    }

    private static MinecraftInstance Official(string modpackId, string version = "1.0") => new()
    {
        Source = InstanceSource.OfficialManifest,
        ModpackId = modpackId,
        ManifestVersion = version,
        Name = "Pack"
    };

    private static (ContentSyncService svc, FakeManifestSource src, List<MinecraftInstance> saved) Make()
    {
        var src = new FakeManifestSource();
        var saved = new List<MinecraftInstance>();
        return (new ContentSyncService(src, saved.Add), src, saved);
    }

    [Fact]
    public async Task NotConfigured_DoesNothing()
    {
        var (svc, src, saved) = Make();
        src.IsConfigured = false;
        Assert.False(await svc.SyncOfficialAsync(new[] { Official("p") }));
        Assert.Empty(saved);
    }

    [Fact]
    public async Task ManualInstances_AreIgnored()
    {
        var (svc, _, saved) = Make();
        var manual = new MinecraftInstance { Source = InstanceSource.Manual, ModpackId = null };
        Assert.False(await svc.SyncOfficialAsync(new[] { manual }));
        Assert.Empty(saved);
    }

    [Fact]
    public async Task AbsentFromSummaries_MarksDiscontinued()
    {
        var (svc, _, saved) = Make(); // resumo vazio → modpack não publicado
        var inst = Official("ghost");

        Assert.True(await svc.SyncOfficialAsync(new[] { inst }));
        Assert.True(inst.IsDiscontinued);
        Assert.Contains(inst, saved);
    }

    [Fact]
    public async Task Republished_ClearsDiscontinued()
    {
        var (svc, src, saved) = Make();
        var inst = Official("p");
        inst.IsDiscontinued = true;
        inst.MetaSyncedAt = DateTime.UtcNow; // já sincronizado, sem mudança de conteúdo
        src.Summaries.Add(new ModpackManifest { Id = "p", Version = "1.0", UpdatedAt = inst.MetaSyncedAt.Value });

        Assert.True(await svc.SyncOfficialAsync(new[] { inst }));
        Assert.False(inst.IsDiscontinued);
        Assert.Equal(0, src.GetManifestCalls); // não precisou do manifesto completo
    }

    [Fact]
    public async Task NewerUpdatedAt_FetchesAndAppliesMetadata_SameVersion()
    {
        var (svc, src, _) = Make();
        var inst = Official("p", "1.0");
        inst.MetaSyncedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        src.Summaries.Add(new ModpackManifest { Id = "p", Version = "1.0", UpdatedAt = newer });
        src.Full["p"] = new ModpackManifest
        {
            Id = "p", Version = "1.0", Name = "Novo Nome",
            Servers = { new ServerEntry { Name = "S", Address = "a", Port = 1 } }
        };

        Assert.True(await svc.SyncOfficialAsync(new[] { inst }));
        Assert.Equal(1, src.GetManifestCalls);
        Assert.Equal("Novo Nome", inst.Name);
        Assert.Single(inst.Servers);
        Assert.Equal(newer, inst.MetaSyncedAt);
    }

    [Fact]
    public async Task UpToDate_DoesNotFetch()
    {
        var (svc, src, saved) = Make();
        var when = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var inst = Official("p");
        inst.MetaSyncedAt = when;
        src.Summaries.Add(new ModpackManifest { Id = "p", Version = "1.0", UpdatedAt = when });

        Assert.False(await svc.SyncOfficialAsync(new[] { inst }));
        Assert.Equal(0, src.GetManifestCalls);
        Assert.Empty(saved);
    }

    [Fact]
    public async Task DifferentVersion_DoesNotApplyMetadata_ButRecordsSync()
    {
        var (svc, src, _) = Make();
        var inst = Official("p", "1.0");
        inst.Name = "Antigo";
        var newer = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        src.Summaries.Add(new ModpackManifest { Id = "p", Version = "2.0", UpdatedAt = newer });
        src.Full["p"] = new ModpackManifest { Id = "p", Version = "2.0", Name = "v2" };

        Assert.True(await svc.SyncOfficialAsync(new[] { inst }));
        Assert.Equal("Antigo", inst.Name);       // metadados de versão nova NÃO aplicados
        Assert.Equal(newer, inst.MetaSyncedAt);   // mas o sync foi registado
    }
}
