using System;
using System.IO;

namespace TCMine_Launcher.Services;

/// <summary>
///     Centraliza todos os caminhos do launcher em <c>%APPDATA%/TCMine-Launcher/</c>.
///     Mantém o disco organizado e evita strings de caminho espalhadas pelo código.
/// </summary>
public static class LauncherPaths
{
    /// <summary>Raiz de dados do launcher (settings, instâncias, jogo).</summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TCMine-Launcher");

    /// <summary>Ficheiro JSON com as definições globais (RAM, Java, versões).</summary>
    public static string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>Estado do jogo em execução (instância + PID) — deteta jogo aberto ao reabrir.</summary>
    public static string RunStateFile => Path.Combine(Root, "running.json");

    /// <summary>Pasta-mãe de todas as instâncias isoladas.</summary>
    public static string InstancesDir => Path.Combine(Root, "instances");

    /// <summary>Pasta de uma instância concreta (config + jogo).</summary>
    public static string InstanceDir(string id) => Path.Combine(InstancesDir, id);

    /// <summary>Ficheiro de configuração JSON de uma instância.</summary>
    public static string InstanceConfigFile(string id) => Path.Combine(InstanceDir(id), "instance.json");

    /// <summary>Diretório <c>.minecraft</c> isolado de uma instância (mods, saves, config).</summary>
    public static string InstanceGameDir(string id) => Path.Combine(InstanceDir(id), "minecraft");

    /// <summary>Ficheiro de log da última execução do jogo desta instância.</summary>
    public static string InstanceLogFile(string id) => Path.Combine(InstanceDir(id), "logs", "latest.log");

    /// <summary>Cache local partilhada de jars de mods (evita re-descarregar entre instâncias).</summary>
    public static string ModCacheDir => Path.Combine(Root, "cache", "mods");

    /// <summary>Cache local de imagens (logos de mods, skins) entre execuções.</summary>
    public static string ImageCacheDir => Path.Combine(Root, "cache", "images");

    /// <summary>Garante que a raiz existe antes de escrever.</summary>
    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
    }
}
