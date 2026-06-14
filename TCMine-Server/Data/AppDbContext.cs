using Microsoft.EntityFrameworkCore;
using TCMine_Server.Data.Entities;

namespace TCMine_Server.Data;

/// <summary>Contexto EF Core (SQLite) com o conteúdo do servidor: novidades, modpacks e releases.</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<NewsEntity> News => Set<NewsEntity>();
    public DbSet<ModpackEntity> Modpacks => Set<ModpackEntity>();
    public DbSet<ModEntryEntity> Mods => Set<ModEntryEntity>();
    public DbSet<ServerEntryEntity> Servers => Set<ServerEntryEntity>();
    public DbSet<ReleaseEntity> Releases => Set<ReleaseEntity>();
    public DbSet<PlayerConfigEntity> PlayerConfigs => Set<PlayerConfigEntity>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Configs do jogador: chave composta (Uuid, ModpackId).
        b.Entity<PlayerConfigEntity>().HasKey(p => new { p.Uuid, p.ModpackId });

        b.Entity<ModpackEntity>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasMany(m => m.Mods)
                .WithOne(x => x.Modpack!)
                .HasForeignKey(x => x.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(m => m.Servers)
                .WithOne(x => x.Modpack!)
                .HasForeignKey(x => x.ModpackId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}