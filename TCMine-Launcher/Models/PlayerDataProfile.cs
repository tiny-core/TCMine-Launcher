using System.Collections.Generic;
using System.IO;

namespace TCMine_Launcher.Models;

/// <summary>
///     Define o conjunto de ficheiros/pastas da pasta do jogo que pertencem ao
///     <b>jogador</b> (e não ao modpack): keybinds (inputs), shader/texturas
///     selecionados e dados de minimapa. Usado para (1) preservar estas configs
///     quando os overrides do modpack são reaplicados num update e (2) sincronizá-las
///     com o servidor entre PCs.
///     Model puro: sem dependências de UI; os caminhos são relativos à pasta
///     <c>minecraft/</c> da instância.
/// </summary>
public static class PlayerDataProfile
{
    /// <summary>
    ///     Padrões (relativos à pasta do jogo) considerados do jogador. Cada padrão é:
    ///     um ficheiro exato, uma pasta (incluída recursivamente) ou um glob simples
    ///     com '*' no último segmento (ex.: <c>shaderpacks/*.txt</c>, <c>config/xaero*</c>).
    ///     Fácil de estender: acrescenta o padrão do mod de minimapa/shader que usares.
    /// </summary>
    public static readonly IReadOnlyList<string> Patterns = new[]
    {
        "options.txt", // keybinds (inputs), vídeo e resource packs selecionados (texturas)
        "optionsshaders.txt", // shader selecionado + toggle (Iris/Oculus)
        "shaderpacks/*.txt", // definições por-shader do jogador
        "XaeroWaypoints", // waypoints do Xaero's Minimap
        "config/xaero*", // config do Xaero's (xaerominimap.txt / pasta xaero/)
        "journeymap" // dados e config do JourneyMap
    };

    /// <summary>
    ///     Caminhos relativos (normalizados com '/') de todos os ficheiros player-owned
    ///     que existem em <paramref name="gameDir" />. Expande pastas recursivamente e
    ///     resolve os globs.
    /// </summary>
    public static IReadOnlyList<string> EnumerateExisting(string gameDir)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) return result;

        foreach (var pattern in Patterns)
            if (pattern.Contains('*'))
                AddGlob(gameDir, pattern, result);
            else
                AddPath(gameDir, pattern, result);

        return result;
    }

    private static void AddPath(string gameDir, string rel, List<string> result)
    {
        var full = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(full))
            result.Add(rel);
        else if (Directory.Exists(full))
            AddDirectory(gameDir, full, result);
    }

    private static void AddGlob(string gameDir, string pattern, List<string> result)
    {
        var slash = pattern.LastIndexOf('/');
        var dirRel = slash >= 0 ? pattern[..slash] : string.Empty;
        var glob = slash >= 0 ? pattern[(slash + 1)..] : pattern;
        var dirFull = Path.Combine(gameDir, dirRel.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(dirFull)) return;

        // Ficheiros e pastas cujo nome casa com o glob (ex.: "xaero*", "*.txt").
        foreach (var entry in Directory.EnumerateFileSystemEntries(dirFull, glob))
            if (File.Exists(entry))
                result.Add(ToRel(gameDir, entry));
            else if (Directory.Exists(entry))
                AddDirectory(gameDir, entry, result);
    }

    private static void AddDirectory(string gameDir, string dirFull, List<string> result)
    {
        foreach (var file in Directory.EnumerateFiles(dirFull, "*", SearchOption.AllDirectories))
            result.Add(ToRel(gameDir, file));
    }

    /// <summary>Caminho relativo à pasta do jogo, normalizado com '/'.</summary>
    private static string ToRel(string gameDir, string full)
    {
        return Path.GetRelativePath(gameDir, full).Replace(Path.DirectorySeparatorChar, '/');
    }
}