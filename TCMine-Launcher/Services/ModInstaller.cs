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
        var completed = 0;

        foreach (var mod in instance.Mods)
        {
            ct.ThrowIfCancellationRequested();

            var dest = Path.Combine(modsDir, mod.FileName);
            if (File.Exists(dest) || string.IsNullOrEmpty(mod.DownloadUrl))
            {
                completed++;
                continue;
            }

            // Progresso global = (mods concluídos + fração do atual) / total.
            var index = completed;
            var fileProgress = new Progress<double>(fraction =>
            {
                var overall = (index + fraction) / total * 100;
                progress.Report(new LaunchProgress(
                    LaunchState.DownloadingAssets, overall,
                    $"A descarregar mod ({index + 1}/{total}): {mod.Name}"));
            });

            progress.Report(new LaunchProgress(
                LaunchState.DownloadingAssets, (double)index / total * 100,
                $"A descarregar mod ({index + 1}/{total}): {mod.Name}"));

            await _client.DownloadAsync(mod.DownloadUrl!, dest, fileProgress, ct);
            completed++;
        }
    }
}
