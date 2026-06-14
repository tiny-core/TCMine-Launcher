using System;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TCMine.Contracts;
using Velopack;

namespace TCMine_Launcher.Services;

/// <summary>
///     Auto-update do launcher via Velopack. O feed de releases é servido pelo
///     servidor TCMine em <c>{ServerUrl}/updates</c>. Só funciona numa instalação
///     feita pelo Velopack (<see cref="UpdateManager.IsInstalled" />) — em
///     desenvolvimento não faz nada.
/// </summary>
public class AppUpdater
{
    private readonly Func<string?> _serverUrlProvider;
    private UpdateManager? _manager;
    private UpdateInfo? _pending;

    public AppUpdater(Func<string?> serverUrlProvider)
    {
        _serverUrlProvider = serverUrlProvider;
    }

    public string? LatestVersion => _pending?.TargetFullRelease.Version.ToString();

    /// <summary>Notas (changelog) da atualização pendente, se o pacote as incluir.</summary>
    public string? LatestNotes => _pending?.TargetFullRelease.NotesMarkdown;

    /// <summary>
    ///     Notas que o admin escreveu no servidor para esta versão (changelog curado),
    ///     servidas em <c>{ServerUrl}/releases/{version}</c>. Null se não houver/erro.
    /// </summary>
    public async Task<string?> GetServerNotesAsync(string version, CancellationToken ct = default)
    {
        var serverUrl = _serverUrlProvider();
        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(version)) return null;

        try
        {
            var url = serverUrl.TrimEnd('/') + "/releases/" + Uri.EscapeDataString(version);
            var dto = await HttpClientProvider.Shared.GetFromJsonAsync<ReleaseDto>(url, ct);
            return dto?.Notes;
        }
        catch
        {
            return null; // sem rede / sem notas no servidor
        }
    }

    /// <summary>Verifica se há atualização. True se houver uma versão mais recente.</summary>
    public async Task<bool> CheckAsync()
    {
        var serverUrl = _serverUrlProvider();
        if (string.IsNullOrWhiteSpace(serverUrl)) return false;

        try
        {
            var feed = serverUrl.TrimEnd('/') + "/updates";
            _manager = new UpdateManager(feed);

            if (!_manager.IsInstalled) return false; // dev / não instalado via Velopack

            _pending = await _manager.CheckForUpdatesAsync();
            return _pending is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Descarrega e aplica a atualização, reiniciando a app.</summary>
    public async Task ApplyAndRestartAsync()
    {
        if (_manager is null || _pending is null) return;
        await _manager.DownloadUpdatesAsync(_pending);
        _manager.ApplyUpdatesAndRestart(_pending);
    }
}