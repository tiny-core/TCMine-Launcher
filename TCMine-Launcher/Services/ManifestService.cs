using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Obtém os modpacks oficiais do servidor TCMine: a lista (<c>/modpacks</c>)
///     e o manifesto completo de um modpack (<c>/modpacks/{id}</c>). Usa o mesmo
///     URL base do servidor (Definições → URL do servidor TCMine).
/// </summary>
public class ManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string?> _baseUrlProvider;
    private readonly HttpClient _http = HttpClientProvider.Shared;

    public ManifestService(Func<string?> baseUrlProvider)
    {
        _baseUrlProvider = baseUrlProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrlProvider());

    private string BaseUrl => (_baseUrlProvider() ?? string.Empty).TrimEnd('/');

    /// <summary>Resumos dos modpacks oficiais disponíveis.</summary>
    public async Task<List<ModpackManifest>> GetModpacksAsync(CancellationToken ct = default)
    {
        EnsureConfigured();
        return await _http.GetFromJsonAsync<List<ModpackManifest>>(
            $"{BaseUrl}/modpacks", JsonOptions, ct) ?? new List<ModpackManifest>();
    }

    /// <summary>
    ///     Manifesto completo de um modpack (mods + servidores). Devolve <c>null</c> se
    ///     o modpack já não existe / foi despublicado (404) — distinto de um erro de
    ///     rede, que é propagado (para não marcar instâncias como descontinuadas à toa).
    /// </summary>
    public async Task<ModpackManifest?> GetManifestAsync(string id, CancellationToken ct = default)
    {
        EnsureConfigured();
        try
        {
            return await _http.GetFromJsonAsync<ModpackManifest>(
                $"{BaseUrl}/modpacks/{id}", JsonOptions, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "URL do servidor TCMine não configurado. Define-o nas Definições.");
    }
}