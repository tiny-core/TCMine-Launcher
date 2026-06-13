using System;
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
    private readonly HttpClient _http = new();

    public CurseForgeClient(Func<string?> baseUrlProvider)
    {
        _baseUrlProvider = baseUrlProvider;
    }

    /// <summary>True se o URL do proxy estiver configurado nas Definições.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrlProvider());

    private string BaseUrl => (_baseUrlProvider() ?? string.Empty).TrimEnd('/');

    /// <summary>Pesquisa mods compatíveis com a versão de MC + NeoForge.</summary>
    public async Task<List<CurseForgeMod>> SearchAsync(
        string gameVersion, string query, int index, CancellationToken ct = default)
    {
        EnsureConfigured();

        // Sem searchFilter o CurseForge devolve apenas os mais populares; com ele,
        // ordena por relevância ao termo (não forçamos sortField para não enterrar
        // a correspondência exata). gameVersion só é enviado se estiver definido.
        var url = $"{BaseUrl}/v1/mods/search" +
                  $"?gameId={MinecraftGameId}" +
                  $"&modLoaderType={NeoForgeLoaderType}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}" +
                  $"&pageSize=30&index={index}";

        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";

        var resp = await _http.GetFromJsonAsync<CurseForgeListResponse<CurseForgeMod>>(url, JsonOptions, ct);
        return resp?.Data ?? new List<CurseForgeMod>();
    }

    /// <summary>Melhor ficheiro (mais recente com download permitido) para a versão dada.</summary>
    public async Task<CurseForgeFile?> GetBestFileAsync(
        int modId, string gameVersion, CancellationToken ct = default)
    {
        EnsureConfigured();
        var url = $"{BaseUrl}/v1/mods/{modId}/files" +
                  $"?gameVersion={Uri.EscapeDataString(gameVersion)}" +
                  $"&modLoaderType={NeoForgeLoaderType}&pageSize=20";

        var resp = await _http.GetFromJsonAsync<CurseForgeListResponse<CurseForgeFile>>(url, JsonOptions, ct);
        return resp?.Data?.FirstOrDefault(f => !string.IsNullOrEmpty(f.DownloadUrl));
    }

    /// <summary>Descarrega um ficheiro do CDN para o caminho indicado.</summary>
    public async Task DownloadAsync(string downloadUrl, string destPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

        using var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(destPath);
        await source.CopyToAsync(file, ct);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "O proxy do CurseForge não está configurado. Define o URL nas Definições.");
    }
}
