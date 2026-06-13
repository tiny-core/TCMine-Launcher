using System.ComponentModel.DataAnnotations;

namespace TCMine_Server.Data.Entities;

/// <summary>Um modpack oficial: metadados + mods + servidores. O <see cref="Id" /> é o slug público.</summary>
public class ModpackEntity
{
    [MaxLength(80)]
    public string Id { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Version { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Minecraft { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Neoforge { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public bool IsPublished { get; set; } = true;

    /// <summary>RAM recomendada (MB) para este modpack; aplicada à instância no install.</summary>
    public int? RecommendedRamMb { get; set; }

    /// <summary>Tem um bundle de overrides (configs/resourcepacks/options) guardado.</summary>
    public bool HasOverrides { get; set; }

    public List<ModEntryEntity> Mods { get; set; } = new();
    public List<ServerEntryEntity> Servers { get; set; } = new();
}

/// <summary>Um mod (CurseForge) pertencente a um modpack.</summary>
public class ModEntryEntity
{
    public int Id { get; set; }

    /// <summary>Id do mod no CurseForge (serializado como "modId" no manifesto público).</summary>
    public long CurseModId { get; set; }

    public long FileId { get; set; }

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>Destino no cliente: "mod", "resourcepack" ou "shaderpack".</summary>
    [MaxLength(20)]
    public string Target { get; set; } = "mod";

    public string ModpackId { get; set; } = string.Empty;
    public ModpackEntity? Modpack { get; set; }
}

/// <summary>Um servidor anunciado por um modpack (escrito no servers.dat pelo launcher).</summary>
public class ServerEntryEntity
{
    public int Id { get; set; }

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    public int Port { get; set; } = 25565;

    public string ModpackId { get; set; } = string.Empty;
    public ModpackEntity? Modpack { get; set; }
}
