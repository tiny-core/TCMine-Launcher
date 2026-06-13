using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Garante que os mods de uma instância estão na pasta <c>mods/</c>: descarrega
///     os que faltam (em paralelo) e verifica o hash SHA-1. Chamado antes do launch.
/// </summary>
public class ModInstaller
{
    private const int MaxParallel = 4;
    private readonly CurseForgeClient _client;

    public ModInstaller(CurseForgeClient client)
    {
        _client = client;
    }

    public async Task EnsureModsAsync(
        MinecraftInstance instance, IProgress<LaunchProgress> progress, CancellationToken ct = default)
    {
        var pending = instance.Mods.Where(m => !string.IsNullOrEmpty(m.DownloadUrl)).ToList();
        if (pending.Count == 0) return;

        var modsDir = Path.Combine(LauncherPaths.InstanceGameDir(instance.Id), "mods");
        Directory.CreateDirectory(modsDir);

        var total = pending.Count;
        var completed = 0;
        using var gate = new SemaphoreSlim(MaxParallel);

        var tasks = pending.Select(async mod =>
        {
            await gate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                var dest = Path.Combine(modsDir, mod.FileName);

                // Já presente e íntegro? Não volta a descarregar.
                if (!IsValid(dest, mod.Sha1))
                {
                    await _client.DownloadAsync(mod.DownloadUrl!, dest, null, ct);

                    if (!IsValid(dest, mod.Sha1))
                    {
                        TryDelete(dest);
                        throw new IOException(
                            $"Mod '{mod.Name}': verificação de integridade (SHA-1) falhou.");
                    }
                }

                var done = Interlocked.Increment(ref completed);
                progress.Report(new LaunchProgress(
                    LaunchState.DownloadingAssets, (double)done / total * 100,
                    $"A descarregar mods ({done}/{total})"));
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>Existe e (se houver hash) o SHA-1 confere.</summary>
    private static bool IsValid(string path, string? sha1)
    {
        if (!File.Exists(path)) return false;
        if (string.IsNullOrEmpty(sha1)) return true; // sem hash conhecido — assume válido

        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA1.HashData(stream));
        return string.Equals(hash, sha1, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* noop */ }
    }
}
