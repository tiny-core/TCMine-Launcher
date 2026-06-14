using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Aplica o bundle de overrides de um modpack oficial (configs, resourcepacks,
///     options.txt, …) na pasta do jogo da instância. Descarrega de
///     <c>{serverUrl}/modpacks/{id}/overrides</c> e extrai uma vez por versão do
///     manifesto (controlado por <see cref="MinecraftInstance.OverridesVersion" />).
/// </summary>
public class OverridesInstaller
{
    private readonly HttpClient _http = HttpClientProvider.Shared;

    public async Task EnsureAsync(MinecraftInstance instance, string? serverUrl, CancellationToken ct = default)
    {
        if (!instance.HasOverrides || string.IsNullOrEmpty(instance.ModpackId)) return;
        if (string.IsNullOrWhiteSpace(serverUrl)) return;
        if (instance.OverridesVersion == instance.ManifestVersion) return; // já aplicado nesta versão

        var url = $"{serverUrl.TrimEnd('/')}/modpacks/{instance.ModpackId}/overrides";
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
            // O servidor não tem overrides para este modpack — marca como tratado.
            instance.OverridesVersion = instance.ManifestVersion;
            return;
        }

        resp.EnsureSuccessStatusCode();

        var gameDir = LauncherPaths.InstanceGameDir(instance.Id);
        Directory.CreateDirectory(gameDir);

        using var buffer = new MemoryStream();
        await using (var net = await resp.Content.ReadAsStreamAsync(ct))
        {
            await net.CopyToAsync(buffer, ct);
        }

        buffer.Position = 0;

        // Numa ATUALIZAÇÃO (já houve overrides aplicados antes) preserva os ficheiros do
        // jogador (keybinds, shader/texturas, minimapa): faz snapshot, deixa os overrides
        // sobrescrever, e repõe o snapshot por cima — o do jogador volta a ganhar. Na
        // primeira instalação (OverridesVersion == null) não há nada a preservar: os
        // overrides fornecem os defaults.
        var snapshotDir = instance.OverridesVersion is not null ? SnapshotPlayerData(gameDir) : null;
        try
        {
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            zip.ExtractToDirectory(gameDir, true);

            if (snapshotDir is not null)
                RestorePlayerData(snapshotDir, gameDir);
        }
        finally
        {
            if (snapshotDir is not null) TryDeleteDir(snapshotDir);
        }

        instance.OverridesVersion = instance.ManifestVersion;
    }

    /// <summary>Copia os ficheiros player-owned existentes para uma pasta temporária.</summary>
    private static string SnapshotPlayerData(string gameDir)
    {
        var temp = Path.Combine(Path.GetTempPath(), "tcmine-cfg-" + Guid.NewGuid().ToString("N"));
        foreach (var rel in PlayerDataProfile.EnumerateExisting(gameDir))
        {
            var src = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
            var dst = Path.Combine(temp, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
        }

        return temp;
    }

    /// <summary>Repõe o snapshot por cima da pasta do jogo (o ficheiro do jogador ganha).</summary>
    private static void RestorePlayerData(string snapshotDir, string gameDir)
    {
        if (!Directory.Exists(snapshotDir)) return;
        foreach (var src in Directory.EnumerateFiles(snapshotDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(snapshotDir, src);
            var dst = Path.Combine(gameDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, true);
        }
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch
        {
            /* noop */
        }
    }
}