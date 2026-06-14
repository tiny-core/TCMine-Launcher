using Microsoft.EntityFrameworkCore;
using TCMine_Server.Data;
using TCMine_Server.Data.Entities;

namespace TCMine_Server.Services;

/// <summary>
///     Leitura/escrita das configs do jogador na BD, por <c>(uuid, modpackId)</c>.
///     Last-write-wins: cada upsert atualiza <see cref="PlayerConfigEntity.UpdatedAt" />.
/// </summary>
public class PlayerConfigStore
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public PlayerConfigStore(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<PlayerConfigEntity?> GetAsync(string uuid, string modpackId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.PlayerConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Uuid == uuid && p.ModpackId == modpackId, ct);
    }

    public async Task<DateTime> UpsertAsync(string uuid, string modpackId, byte[] zip, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var existing = await db.PlayerConfigs
            .FirstOrDefaultAsync(p => p.Uuid == uuid && p.ModpackId == modpackId, ct);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            db.PlayerConfigs.Add(new PlayerConfigEntity
            {
                Uuid = uuid, ModpackId = modpackId, Data = zip, UpdatedAt = now
            });
        }
        else
        {
            existing.Data = zip;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return now;
    }
}