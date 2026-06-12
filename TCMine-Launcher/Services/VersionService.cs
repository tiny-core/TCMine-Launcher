using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Installer.NeoForge.Versions;

namespace TCMine_Launcher.Services;

/// <summary>
///     Obtém as versões disponíveis a partir das APIs oficiais via CmlLib:
///     releases do Minecraft (manifesto da Mojang, filtrado) e versões do NeoForge
///     compatíveis com uma versão de MC.
/// </summary>
public class VersionService
{
    private readonly HttpClient _http = new();

    /// <summary>Lista apenas as <b>releases</b> do Minecraft (exclui snapshots/betas).</summary>
    public async Task<List<string>> GetMinecraftReleasesAsync()
    {
        var launcher = new MinecraftLauncher(LauncherPaths.Root);
        var versions = await launcher.GetAllVersionsAsync();

        var releases = new List<string>();
        foreach (var v in versions)
            if (v.Type == "release")
                releases.Add(v.Name);

        return releases;
    }

    /// <summary>Versões do NeoForge compatíveis com a versão de Minecraft indicada.</summary>
    public async Task<List<string>> GetNeoForgeVersionsAsync(string mcVersion)
    {
        var loader = new NeoForgeVersionLoader(_http);
        var versions = await loader.GetNeoForgeVersions(mcVersion);
        return versions.Select(v => v.VersionName).ToList();
    }
}
