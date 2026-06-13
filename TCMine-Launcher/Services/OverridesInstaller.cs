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
            await net.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
        zip.ExtractToDirectory(gameDir, overwriteFiles: true);

        instance.OverridesVersion = instance.ManifestVersion;
    }
}
