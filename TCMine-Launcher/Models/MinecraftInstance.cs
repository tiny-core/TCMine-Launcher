using System;
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

    /// <summary>Versão do manifesto instalada (só para instâncias oficiais — fase 3).</summary>
    public string? ManifestVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastPlayedAt { get; set; }

    // ── Lógica de domínio pura (calculada — não persistir) ───────────
    [JsonIgnore]
    public string VersionSummary => $"MC {MinecraftVersion} · NeoForge {NeoForgeVersion}";

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
}
