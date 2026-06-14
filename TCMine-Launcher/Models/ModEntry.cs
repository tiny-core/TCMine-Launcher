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

    /// <summary>Nome do ficheiro — também a chave de "já instalado" na pasta de destino.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     Onde o ficheiro é instalado: <c>mod</c> (pasta <c>mods/</c>),
    ///     <c>resourcepack</c> (<c>resourcepacks/</c>) ou <c>shaderpack</c>
    ///     (<c>shaderpacks/</c>). Default <c>mod</c> (compatível com dados antigos).
    /// </summary>
    public string Target { get; set; } = "mod";

    /// <summary>URL direto do CDN do CurseForge (pode ser nulo se a distribuição for proibida).</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Hash SHA-1 para verificar a integridade após o download.</summary>
    public string? Sha1 { get; set; }

    /// <summary>URL do logótipo do mod (persistido para a imagem reaparecer).</summary>
    public string? LogoUrl { get; set; }
}