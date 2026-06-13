using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>Verifica se há uma versão mais recente do launcher (<c>/launcher/latest</c>).</summary>
public class UpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<string?> _baseUrlProvider;
    private readonly HttpClient _http = HttpClientProvider.Shared;

    public UpdateService(Func<string?> baseUrlProvider)
    {
        _baseUrlProvider = baseUrlProvider;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_baseUrlProvider());

    private string BaseUrl => (_baseUrlProvider() ?? string.Empty).TrimEnd('/');

    public async Task<LauncherUpdate?> GetLatestAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            return await _http.GetFromJsonAsync<LauncherUpdate>(
                $"{BaseUrl}/launcher/latest", JsonOptions, ct);
        }
        catch
        {
            return null;
        }
    }
}
