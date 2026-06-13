namespace TCMine_Launcher.Models;

/// <summary>Informação da versão mais recente do launcher, vinda do servidor.</summary>
public class LauncherUpdate
{
    public string Version { get; set; } = string.Empty;

    /// <summary>URL da página/ficheiro de download da nova versão.</summary>
    public string Url { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
