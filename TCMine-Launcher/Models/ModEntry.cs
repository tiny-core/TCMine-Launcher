namespace TCMine_Launcher.Models;

/// <summary>
///     Um mod escolhido para uma instância (proveniente do CurseForge).
///     Model puro e serializável — guardado na lista <see cref="MinecraftInstance.Mods" />.
///     O download real acontece na instalação/launch a partir de <see cref="DownloadUrl" />.
/// </summary>
public class ModEntry
{
    public int ModId { get; set; }
    public int FileId { get; set; }

    /// <summary>Nome legível do mod (para a UI).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Nome do ficheiro .jar — também a chave de "já instalado" na pasta mods.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>URL direto do CDN do CurseForge (pode ser nulo se a distribuição for proibida).</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Hash SHA-1 para verificar a integridade após o download.</summary>
    public string? Sha1 { get; set; }
}
