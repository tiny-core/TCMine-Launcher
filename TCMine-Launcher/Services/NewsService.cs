using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>Lê as novidades publicadas pelo servidor TCMine (<c>/news</c>).</summary>
public class NewsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string?> _baseUrlProvider;
    private readonly HttpClient _http = HttpClientProvider.Shared;

    public NewsService(Func<string?> baseUrlProvider)
    {
        _baseUrlProvider = baseUrlProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrlProvider());

    private string BaseUrl => (_baseUrlProvider() ?? string.Empty).TrimEnd('/');

    public async Task<List<NewsItem>> GetNewsAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "URL do servidor TCMine não configurado. Define-o nas Definições.");

        return await _http.GetFromJsonAsync<List<NewsItem>>(
            $"{BaseUrl}/news", JsonOptions, ct) ?? new List<NewsItem>();
    }
}