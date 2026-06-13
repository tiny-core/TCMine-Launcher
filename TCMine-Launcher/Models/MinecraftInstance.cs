using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TCMine_Launcher.Models;

/// <summary>Origem de uma instância — criada à mão ou derivada do manifesto oficial.</summary>
public enum InstanceSource
{
    Manual,
    OfficialManifest
}

/// <summary>
///     Uma instância de jogo autossuficiente: versão, loader e (na fase 4) mods e
///     saves próprios, isolada numa pasta dedicada. É o conceito central do launcher
///     — tudo o que se lança é uma instância.
///     Model puro: serializável, sem dependências de UI.
/// </summary>
public class MinecraftInstance
{
    /// <summary>Identificador estável; também é o nome da pasta da instância.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "Nova Instância";

    public string MinecraftVersion { get; set; } = "1.21.1";
    public string NeoForgeVersion { get; set; } = "21.1.172";

    public InstanceSource Source { get; set; } = InstanceSource.Manual;

    /// <summary>RAM específica desta instância; <c>null</c> = usa o default global.</summary>
    public int? RamOverrideMb { get; set; }

    /// <summary>Caminho do Java específico; <c>null</c>/vazio = global ou automático.</summary>
    public string? JavaPathOverride { get; set; }

    /// <summary>Id do modpack oficial de origem (null se criada à mão).</summary>
    public string? ModpackId { get; set; }

    /// <summary>Versão do manifesto instalada (só para instâncias oficiais).</summary>
    public string? ManifestVersion { get; set; }

    /// <summary>Descrição do modpack (vinda do servidor, para instâncias oficiais).</summary>
    public string? Description { get; set; }

    /// <summary>O modpack oficial tem um bundle de overrides a aplicar.</summary>
    public bool HasOverrides { get; set; }

    /// <summary>Versão do manifesto cujos overrides já foram aplicados (evita reaplicar).</summary>
    public string? OverridesVersion { get; set; }

    /// <summary>Mods (CurseForge) associados a esta instância.</summary>
    public List<ModEntry> Mods { get; set; } = new();

    /// <summary>Servidores associados — escritos no servers.dat ao instalar.</summary>
    public List<ServerEntry> Servers { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastPlayedAt { get; set; }

    // ── Lógica de domínio pura (calculada — não persistir) ───────────
    [JsonIgnore]
    public string VersionSummary =>
        string.IsNullOrWhiteSpace(ManifestVersion)
            ? $"MC {MinecraftVersion} · NeoForge {NeoForgeVersion}"
            : $"v{ManifestVersion} · MC {MinecraftVersion} · NeoForge {NeoForgeVersion}";

    /// <summary>Etiqueta curta para o "tag" do cartão.</summary>
    [JsonIgnore]
    public string SourceLabel => Source switch
    {
        InstanceSource.OfficialManifest => "OFICIAL",
        _ => "PERSONALIZADA"
    };

    /// <summary>Inicial em maiúscula para o ícone do cartão.</summary>
    [JsonIgnore]
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..1].ToUpper();

    /// <summary>Instâncias oficiais não podem ser editadas/eliminadas livremente.</summary>
    [JsonIgnore]
    public bool IsOfficial => Source == InstanceSource.OfficialManifest;

    /// <summary>Resumo do número de mods para listas/cartões.</summary>
    [JsonIgnore]
    public string ModsLabel => Mods.Count == 0 ? "Sem mods" : $"{Mods.Count} mod(s)";
}
