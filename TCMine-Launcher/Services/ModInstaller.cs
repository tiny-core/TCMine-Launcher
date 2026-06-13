using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Garante que os mods de uma instância estão presentes na pasta <c>mods/</c>,
///     descarregando os que faltam via <see cref="CurseForgeClient" />. Chamado no
///     fluxo de instalação/launch, antes de arrancar o jogo.
/// </summary>
public class ModInstaller
{
    private readonly CurseForgeClient _client;

    public ModInstaller(CurseForgeClient client)
    {
        _client = client;
    }

    /// <summary>Descarrega os mods em falta da instância. Sem mods, não faz nada.</summary>
    public async Task EnsureModsAsync(
        MinecraftInstance instance, IProgress<LaunchProgress> progress, CancellationToken ct = default)
    {
        if (instance.Mods.Count == 0) return;

        var modsDir = Path.Combine(LauncherPaths.InstanceGameDir(instance.Id), "mods");
        Directory.CreateDirectory(modsDir);

        var total = instance.Mods.Count;
        var done = 0;

        foreach (var mod in instance.Mods)
        {
            ct.ThrowIfCancellationRequested();
            done++;

            var dest = Path.Combine(modsDir, mod.FileName);
            if (File.Exists(dest) || string.IsNullOrEmpty(mod.DownloadUrl))
                continue;

            var pct = (double)done / total * 100;
            progress.Report(new LaunchProgress(
                LaunchState.DownloadingAssets, pct, $"A descarregar mod: {mod.Name}"));

            await _client.DownloadAsync(mod.DownloadUrl!, dest, ct);
        }
    }
}
