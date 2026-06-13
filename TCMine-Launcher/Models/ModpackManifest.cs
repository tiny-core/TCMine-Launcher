using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TCMine_Launcher.Models;

/// <summary>
///     Manifesto de um modpack oficial servido pelo servidor TCMine. Na lista
///     (<c>/modpacks</c>) vem só o resumo (sem mods); no detalhe
///     (<c>/modpacks/{id}</c>) traz os mods e servidores.
/// </summary>
public class ModpackManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Minecraft { get; set; } = string.Empty;
    public string Neoforge { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>O modpack tem um bundle de overrides (configs/resourcepacks/options).</summary>
    public bool HasOverrides { get; set; }

    /// <summary>RAM recomendada (MB) pelo modpack; aplicada à instância ao instalar.</summary>
    public int? RecommendedRamMb { get; set; }

    /// <summary>Nº de mods (preenchido no resumo da lista, quando Mods vem vazio).</summary>
    public int ModCount { get; set; }

    /// <summary>Nº de servidores (preenchido no resumo da lista).</summary>
    public int ServerCount { get; set; }

    public List<ModEntry> Mods { get; set; } = new();
    public List<ServerEntry> Servers { get; set; } = new();

    [JsonIgnore]
    public int TotalMods => Mods.Count > 0 ? Mods.Count : ModCount;

    [JsonIgnore]
    public int TotalServers => Servers.Count > 0 ? Servers.Count : ServerCount;

    [JsonIgnore]
    public bool HasServer => TotalServers > 0;

    [JsonIgnore]
    public string ServerLabel => HasServer ? "Com servidor" : "Sem servidor";

    [JsonIgnore]
    public string VersionSummary =>
        string.IsNullOrWhiteSpace(Version)
            ? $"MC {Minecraft} · NeoForge {Neoforge}"
            : $"v{Version} · MC {Minecraft} · NeoForge {Neoforge}";

    [JsonIgnore]
    public string ModsSummary => TotalMods == 0 ? "Sem mods" : $"{TotalMods} mod(s)";
}
