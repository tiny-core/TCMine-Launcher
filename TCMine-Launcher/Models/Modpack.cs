namespace TCMine_Launcher.Models;

/// <summary>
///     Dados puros de um modpack — sem dependências de UI.
///     Pode ser serializado, carregado de uma API/disco, comparado, etc.
/// </summary>
public class Modpack
{
    public string Name { get; set; } = "TCMine Modpack";
    public string Author { get; set; } = "TCMine";
    public string Version { get; set; } = "1.0.0";
    public string MinecraftVersion { get; set; } = "1.21.1";
    public string NeoForgeVersion { get; set; } = "21.1.172";
    public string Tagline { get; set; } = "Modpack oficial";
    public string Description { get; set; } = string.Empty;
    public bool IsInstalled { get; set; }

    /// <summary>Versões compactas para mostrar em chips/listas.</summary>
    public string VersionSummary => $"MC {MinecraftVersion} · NeoForge {NeoForgeVersion}";

    public string AuthorLabel => $"por {Author}";

    public string InstallLabel => IsInstalled ? "Jogar" : "Instalar";
}
