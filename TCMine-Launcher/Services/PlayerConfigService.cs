using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Sincroniza as configs do jogador (keybinds, shader/texturas, minimapa — ver
///     <see cref="PlayerDataProfile" />) com o servidor TCMine, por
///     <c>(uuid, modpackId)</c>. Permite repor as configs ao entrar noutro PC.
///     Só atua em instâncias oficiais com conta Microsoft (UUID). Todas as falhas
///     são best-effort (não bloqueiam o jogo). Last-write-wins por timestamp.
/// </summary>
public class PlayerConfigService
{
    private readonly HttpClient _http = HttpClientProvider.Shared;

    /// <summary>
    ///     Descarrega as configs do servidor e repõe-nas na pasta do jogo se forem mais
    ///     recentes do que as já aplicadas localmente. Chamado antes de lançar o jogo.
    /// </summary>
    public async Task PullAsync(MinecraftInstance instance, string? uuid, string? accessToken,
        string? serverUrl, CancellationToken ct = default)
    {
        if (!ShouldSync(instance, uuid, accessToken, serverUrl)) return;

        try
        {
            var url = BuildUrl(serverUrl!, uuid!, instance.ModpackId!);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.StatusCode == HttpStatusCode.NotFound) return; // nada guardado no servidor
            resp.EnsureSuccessStatusCode();

            var serverTime = resp.Content.Headers.LastModified;
            var localTime = ReadSidecar(instance.Id);

            // Só sobrepõe o local se o servidor for estritamente mais recente.
            if (serverTime is not null && localTime is not null && serverTime <= localTime) return;

            var gameDir = LauncherPaths.InstanceGameDir(instance.Id);
            Directory.CreateDirectory(gameDir);

            using var buffer = new MemoryStream();
            await using (var net = await resp.Content.ReadAsStreamAsync(ct))
            {
                await net.CopyToAsync(buffer, ct);
            }

            buffer.Position = 0;

            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Read))
            {
                zip.ExtractToDirectory(gameDir, true);
            }

            if (serverTime is not null) WriteSidecar(instance.Id, serverTime.Value);
            Log.Information("Configs do jogador repostas do servidor (instância {Name})", instance.Name);
        }
        catch (OperationCanceledException)
        {
            throw; // cancelamento do launch propaga normalmente
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao obter configs do jogador (ignorado)");
        }
    }

    /// <summary>
    ///     Zipa as configs do jogador da pasta do jogo e envia para o servidor. Chamado
    ///     quando o jogo fecha (captura keybinds/waypoints alterados na sessão).
    /// </summary>
    public async Task PushAsync(MinecraftInstance instance, string? uuid, string? accessToken,
        string? serverUrl, CancellationToken ct = default)
    {
        if (!ShouldSync(instance, uuid, accessToken, serverUrl)) return;

        try
        {
            var gameDir = LauncherPaths.InstanceGameDir(instance.Id);
            var files = PlayerDataProfile.EnumerateExisting(gameDir);
            if (files.Count == 0) return;

            using var buffer = new MemoryStream();
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
            {
                foreach (var rel in files)
                {
                    var full = Path.Combine(gameDir, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full)) zip.CreateEntryFromFile(full, rel);
                }
            }

            buffer.Position = 0;

            var url = BuildUrl(serverUrl!, uuid!, instance.ModpackId!);
            using var content = new StreamContent(buffer);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            using var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            // Marca o timestamp devolvido como aplicado (evita re-pull do que enviámos).
            var updatedAt = ParseUpdatedAt(await resp.Content.ReadAsStringAsync(ct));
            if (updatedAt is not null) WriteSidecar(instance.Id, updatedAt.Value);
            Log.Information("Configs do jogador enviadas para o servidor (instância {Name})", instance.Name);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao enviar configs do jogador (ignorado)");
        }
    }

    private static bool ShouldSync(MinecraftInstance instance, string? uuid, string? accessToken,
        string? serverUrl)
    {
        return instance.IsOfficial && !string.IsNullOrEmpty(instance.ModpackId)
                                   && !string.IsNullOrEmpty(uuid) && !string.IsNullOrEmpty(accessToken)
                                   && !string.IsNullOrWhiteSpace(serverUrl);
    }

    private static string BuildUrl(string serverUrl, string uuid, string modpackId)
    {
        return $"{serverUrl.TrimEnd('/')}/players/{uuid}/configs/{modpackId}";
    }

    // ── Sidecar (timestamp das configs já aplicadas) ─────────────
    private static DateTimeOffset? ReadSidecar(string instanceId)
    {
        try
        {
            var path = LauncherPaths.ConfigSyncFile(instanceId);
            return File.Exists(path) ? ParseUpdatedAt(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null; // sidecar corrompido — trata como inexistente
        }
    }

    private static void WriteSidecar(string instanceId, DateTimeOffset updatedAt)
    {
        try
        {
            var path = LauncherPaths.ConfigSyncFile(instanceId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{{\"updatedAt\":\"{updatedAt.UtcDateTime:O}\"}}");
        }
        catch
        {
            /* best-effort */
        }
    }

    private static DateTimeOffset? ParseUpdatedAt(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("updatedAt", out var v) &&
                v.TryGetDateTimeOffset(out var dto))
                return dto;
        }
        catch
        {
            /* ignora */
        }

        return null;
    }
}