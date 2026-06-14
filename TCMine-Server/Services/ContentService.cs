using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TCMine_Server.Data;

namespace TCMine_Server.Services;

/// <summary>
///     Leitura do conteúdo público a partir da BD, projetado para os DTOs do
///     contrato do launcher (<c>/news</c>, <c>/modpacks</c>, <c>/modpacks/{id}</c>).
/// </summary>
public class ContentService
{
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly OverridesStore _overrides;

    public ContentService(IDbContextFactory<AppDbContext> factory, OverridesStore overrides)
    {
        _factory = factory;
        _overrides = overrides;
    }

    public async Task<List<NewsDto>> GetNewsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var items = await db.News
            .Where(n => n.IsPublished)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync(ct);

        return items
            .Select(n => new NewsDto(n.Tag, n.Title, FormatDate(n.PublishedAt), n.Summary))
            .ToList();
    }

    public async Task<List<ModpackSummaryDto>> GetModpackSummariesAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Modpacks
            .Where(m => m.IsPublished)
            .OrderBy(m => m.Name)
            .Select(m => new ModpackSummaryDto(
                m.Id, m.Name, m.Version, m.Minecraft, m.Neoforge, m.Description,
                m.Mods.Count, m.Servers.Count))
            .ToListAsync(ct);
    }

    public async Task<ModpackManifestDto?> GetManifestAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var m = await db.Modpacks
            .Include(x => x.Mods)
            .Include(x => x.Servers)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, ct);

        if (m is null) return null;

        // HasOverrides deriva do ficheiro real (evita dessincronizar do bool manual).
        return new ModpackManifestDto(
            m.Id, m.Name, m.Version, m.Minecraft, m.Neoforge, m.Description, _overrides.Exists(m.Id),
            m.RecommendedRamMb,
            m.Mods.Select(x => new ModDto(x.CurseModId, x.FileId, x.Name, x.FileName, x.DownloadUrl,
                string.IsNullOrEmpty(x.Target) ? "mod" : x.Target)).ToList(),
            m.Servers.Select(x => new ServerDto(x.Name, x.Address, x.Port)).ToList());
    }

    /// <summary>Formata a data no estilo das novidades (ex.: "07 jun 2026").</summary>
    private static string FormatDate(DateTime dt)
    {
        return dt.ToString("dd MMM yyyy", Pt).Replace(".", string.Empty);
    }
}