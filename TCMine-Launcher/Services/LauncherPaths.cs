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

    /// <summary>Pasta onde, na fase 2, viverão as instâncias isoladas.</summary>
    public static string InstancesDir => Path.Combine(Root, "instances");

    /// <summary>
    ///     Pasta do jogo na fase 1 (instância única partilhada). Quando as
    ///     instâncias chegarem, cada uma terá o seu próprio diretório aqui dentro.
    /// </summary>
    public static string DefaultGameDir => Path.Combine(Root, "minecraft");

    /// <summary>Garante que a raiz existe antes de escrever.</summary>
    public static void EnsureRoot()
    {
        Directory.CreateDirectory(Root);
    }
}
