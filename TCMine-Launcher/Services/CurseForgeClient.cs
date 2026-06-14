using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TCMine_Launcher.Services;

/// <summary>
///     Cliente do CurseForge que fala com o <b>proxy do servidor TCMine</b> (que injeta
///     a API key e reencaminha para <c>api.curseforge.com/v1/*</c>). A key nunca sai do
///     servidor. O URL base do proxy vem das Definições (pode estar vazio = não configurado).
///     O download dos .jar é feito direto do CDN público do CurseForge (<c>DownloadUrl</c>).
/// </summary>
public class CurseForgeClient
{
    private const int MinecraftGameId = 432;
    private const int NeoForgeLoaderType = 6; // CurseForge: 6 = NeoForge

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string?> _baseUrlProvider;
    private readonly ConcurrentDictionary<string, List<CurseForgeFile>> _filesCache = new();
    private readonly HttpClient _http = HttpClientProvider.Shared;
    private readonly ConcurrentDictionary<int, CurseForgeMod?> _modCache = new();

    // Cache em memória (por sessão) — evita repetir pedidos iguais.
    private readonly ConcurrentDictionary<string, List<CurseForgeMod>> _searchCache = new();

    public CurseForgeClient(Func<string?> baseUrlProvider)
    {
        _baseUrlProvider = baseUrlProvider;
    }

    /// <summary>True se o URL do proxy estiver configurado nas Definições.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrlProvider());

    private string BaseUrl => (_baseUrlProvider() ?? string.Empty).TrimEnd('/');

    /// <summary>
    ///     Pesquisa mods POR NOME (sem filtrar por versão/loader, para o termo
    ///     aparecer mesmo que a compatibilidade só seja avaliada ao adicionar).
    ///     Ordena por popularidade. Resultados em cache por sessão.
    /// </summary>
    public async Task<List<CurseForgeMod>> SearchAsync(
        string query, int index, CancellationToken ct = default)
    {
        EnsureConfigured();

        var cacheKey = $"{query}|{index}";
        if (_searchCache.TryGetValue(cacheKey, out var cached)) return cached;

        var url = $"{BaseUrl}/v1/mods/search" +
                  $"?gameId={MinecraftGameId}" +
                  $"&classId=6" + // 6 = categoria "Mods"
                  $"&searchFilter={Uri.EscapeDataString(query)}" +
                  $"&sortField=2&sortOrder=desc&pageSize=30&index={index}";

        var resp = await _http.GetFromJsonAsync<CurseForgeListResponse<CurseForgeMod>>(url, JsonOptions, ct);
        var list = resp?.Data ?? new List<CurseForgeMod>();
        _searchCache[cacheKey] = list;
        return list;
    }

    /// <summary>
    ///     Ficheiros (mais recentes primeiro) de um mod compatíveis com a versão dada.
    ///     Em cache por sessão — uma chamada serve para "latest" e para resolver uma
    ///     versão específica, evitando repetir pedidos ao CurseForge.
    /// </summary>
    public async Task<List<CurseForgeFile>> GetFilesAsync(
        int modId, string gameVersion, CancellationToken ct = default)
    {
        EnsureConfigured();

        var cacheKey = $"{modId}|{gameVersion}";
        if (_filesCache.TryGetValue(cacheKey, out var cached)) return cached;

        var url = $"{BaseUrl}/v1/mods/{modId}/files" +
                  $"?gameVersion={Uri.EscapeDataString(gameVersion)}" +
                  $"&modLoaderType={NeoForgeLoaderType}&pageSize=50";

        var resp = await _http.GetFromJsonAsync<CurseForgeListResponse<CurseForgeFile>>(url, JsonOptions, ct);
        var list = resp?.Data ?? new List<CurseForgeFile>();
        _filesCache[cacheKey] = list;
        return list;
    }

    /// <summary>Melhor ficheiro (mais recente com download permitido) para a versão dada.</summary>
    public async Task<CurseForgeFile?> GetBestFileAsync(
        int modId, string gameVersion, CancellationToken ct = default)
    {
        var files = await GetFilesAsync(modId, gameVersion, ct);
        return files.FirstOrDefault(f => !string.IsNullOrEmpty(f.DownloadUrl));
    }

    /// <summary>Detalhe de um mod (usado para o nome das dependências). Em cache.</summary>
    public async Task<CurseForgeMod?> GetModAsync(int modId, CancellationToken ct = default)
    {
        EnsureConfigured();

        if (_modCache.TryGetValue(modId, out var cached)) return cached;

        var resp = await _http.GetFromJsonAsync<CurseForgeSingleResponse<CurseForgeMod>>(
            $"{BaseUrl}/v1/mods/{modId}", JsonOptions, ct);
        _modCache[modId] = resp?.Data;
        return resp?.Data;
    }

    /// <summary>
    ///     Descarrega um ficheiro do CDN para o caminho indicado, reportando a
    ///     fração concluída (0..1) quando o tamanho é conhecido.
    /// </summary>
    public async Task DownloadAsync(
        string downloadUrl, string destPath,
        IProgress<double>? fileProgress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        using var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destPath);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            if (total > 0) fileProgress?.Report((double)received / total);
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "O proxy do CurseForge não está configurado. Define o URL nas Definições.");
    }
}