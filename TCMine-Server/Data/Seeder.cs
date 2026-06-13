using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCMine_Server.Data.Entities;

namespace TCMine_Server.Data;

/// <summary>
///     Importa o conteúdo dos ficheiros JSON legados (news.json, modpacks/*.json)
///     para a BD na primeira execução (tabelas vazias), para não perder os dados
///     que já existiam antes da migração para SQLite.
/// </summary>
public static class Seeder
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-PT");

    public static async Task SeedAsync(AppDbContext db, string contentRoot, ILogger logger)
    {
        await SeedNewsAsync(db, Path.Combine(contentRoot, "news.json"), logger);
        await SeedModpacksAsync(db, Path.Combine(contentRoot, "modpacks"), logger);
    }

    private static async Task SeedNewsAsync(AppDbContext db, string file, ILogger logger)
    {
        if (await db.News.AnyAsync() || !File.Exists(file)) return;

        try
        {
            var items = JsonSerializer.Deserialize<List<SeedNews>>(await File.ReadAllTextAsync(file), Json)
                        ?? new List<SeedNews>();

            // O ficheiro está do mais recente para o mais antigo; preserva a ordem
            // via PublishedAt decrescente quando a data não for parseável.
            var now = DateTime.UtcNow;
            for (var i = 0; i < items.Count; i++)
            {
                var n = items[i];
                db.News.Add(new NewsEntity
                {
                    Tag = n.Tag ?? string.Empty,
                    Title = n.Title ?? string.Empty,
                    Summary = n.Summary ?? string.Empty,
                    PublishedAt = ParseDate(n.Date) ?? now.AddDays(-i),
                    IsPublished = true
                });
            }

            await db.SaveChangesAsync();
            logger.LogInformation("Seed: {Count} novidades importadas.", items.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Seed de novidades falhou (ignorado).");
        }
    }

    private static async Task SeedModpacksAsync(AppDbContext db, string dir, ILogger logger)
    {
        if (await db.Modpacks.AnyAsync() || !Directory.Exists(dir)) return;

        var count = 0;
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var m = JsonSerializer.Deserialize<SeedModpack>(await File.ReadAllTextAsync(path), Json);
                if (m is null || string.IsNullOrWhiteSpace(m.Id)) continue;

                db.Modpacks.Add(new ModpackEntity
                {
                    Id = m.Id,
                    Name = m.Name ?? string.Empty,
                    Version = m.Version ?? string.Empty,
                    Minecraft = m.Minecraft ?? string.Empty,
                    Neoforge = m.Neoforge ?? string.Empty,
                    Description = m.Description ?? string.Empty,
                    IsPublished = true,
                    Mods = (m.Mods ?? new()).Select(x => new ModEntryEntity
                    {
                        CurseModId = x.ModId, FileId = x.FileId, Name = x.Name ?? string.Empty,
                        FileName = x.FileName ?? string.Empty, DownloadUrl = x.DownloadUrl ?? string.Empty
                    }).ToList(),
                    Servers = (m.Servers ?? new()).Select(x => new ServerEntryEntity
                    {
                        Name = x.Name ?? string.Empty, Address = x.Address ?? string.Empty,
                        Port = x.Port == 0 ? 25565 : x.Port
                    }).ToList()
                });
                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Seed do modpack {File} falhou (ignorado).", path);
            }
        }

        await db.SaveChangesAsync();
        if (count > 0) logger.LogInformation("Seed: {Count} modpack(s) importado(s).", count);
    }

    private static DateTime? ParseDate(string? s) =>
        DateTime.TryParse(s, Pt, DateTimeStyles.None, out var d)
        || DateTime.TryParseExact(s, "dd MMM yyyy", Pt, DateTimeStyles.None, out d)
            ? d
            : null;

    private sealed record SeedNews(string? Tag, string? Title, string? Date, string? Summary);

    private sealed record SeedModpack(
        string? Id, string? Name, string? Version, string? Minecraft, string? Neoforge,
        string? Description, List<SeedMod>? Mods, List<SeedServer>? Servers);

    private sealed record SeedMod(long ModId, long FileId, string? Name, string? FileName, string? DownloadUrl);

    private sealed record SeedServer(string? Name, string? Address, int Port);
}
