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
        MinecraftInstance instance, IProgress<LaunchProgress> progress,
        CancellationToken ct = default, bool prune = false)
    {
        var gameDir = LauncherPaths.InstanceGameDir(instance.Id);
        var modsDir = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(modsDir);

        // Diff: remove jars que já não fazem parte do modpack (só em instâncias geridas).
        if (prune) PruneUnlisted(instance, modsDir);

        var pending = instance.Mods.Where(m => !string.IsNullOrEmpty(m.DownloadUrl)).ToList();
        if (pending.Count == 0) return;

        var total = pending.Count;
        var completed = 0;
        using var gate = new SemaphoreSlim(MaxParallel);

        var tasks = pending.Select(async mod =>
        {
            await gate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                // Cada ficheiro vai para a pasta certa: mods / resourcepacks / shaderpacks.
                var dest = Path.Combine(gameDir, FolderFor(mod.Target), mod.FileName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                // Já presente e íntegro na instância? Não faz nada.
                if (!IsValid(dest, mod.Sha1))
                {
                    var cached = Path.Combine(LauncherPaths.ModCacheDir, mod.FileName);

                    // Na cache global e íntegro? Copia em vez de descarregar.
                    if (!IsValid(cached, mod.Sha1))
                    {
                        Directory.CreateDirectory(LauncherPaths.ModCacheDir);
                        await _client.DownloadAsync(mod.DownloadUrl!, cached, null, ct);

                        if (!IsValid(cached, mod.Sha1))
                        {
                            TryDelete(cached);
                            throw new IOException(
                                $"Mod '{mod.Name}': verificação de integridade (SHA-1) falhou.");
                        }
                    }

                    File.Copy(cached, dest, overwrite: true);
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

    /// <summary>Pasta de destino conforme o tipo do ficheiro.</summary>
    private static string FolderFor(string? target) => target?.ToLowerInvariant() switch
    {
        "resourcepack" => "resourcepacks",
        "shaderpack" => "shaderpacks",
        _ => "mods"
    };

    /// <summary>Apaga jars na pasta mods que não constam da lista da instância.</summary>
    private static void PruneUnlisted(MinecraftInstance instance, string modsDir)
    {
        var wanted = instance.Mods
            .Select(m => m.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var jar in Directory.EnumerateFiles(modsDir, "*.jar"))
            if (!wanted.Contains(Path.GetFileName(jar)))
                TryDelete(jar);
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
