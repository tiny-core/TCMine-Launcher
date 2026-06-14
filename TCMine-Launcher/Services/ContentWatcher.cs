using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace TCMine_Launcher.Services;

/// <summary>Estado da ligação ao stream de eventos do servidor.</summary>
public enum ServerConnectionState
{
    /// <summary>Sem servidor configurado ou ligação em baixo.</summary>
    Offline,

    /// <summary>A tentar (re)estabelecer a ligação.</summary>
    Connecting,

    /// <summary>Ligado e a receber eventos.</summary>
    Connected
}

/// <summary>
///     Liga-se ao stream SSE do servidor (<c>/events</c>) e dispara
///     <see cref="ContentChanged" /> sempre que o conteúdo público muda (novidades ou
///     modpacks editados no admin). Reconecta sozinho se a ligação cair ou o servidor
///     reiniciar. O evento é entregue no contexto de sincronização capturado na
///     construção (a thread da UI), para as ViewModels poderem atualizar coleções.
/// </summary>
public class ContentWatcher
{
    private readonly Func<string?> _baseUrlProvider;
    private readonly HttpClient _http = HttpClientProvider.Shared;
    private readonly SynchronizationContext? _sync = SynchronizationContext.Current;
    private CancellationTokenSource? _cts;
    private long _lastVersion = -1;

    public ContentWatcher(Func<string?> baseUrlProvider)
    {
        _baseUrlProvider = baseUrlProvider;
    }

    /// <summary>Disparado (na thread da UI) quando o servidor anuncia uma versão nova.</summary>
    public event Action? ContentChanged;

    /// <summary>Disparado (na thread da UI) quando o estado da ligação muda.</summary>
    public event Action? ConnectionChanged;

    /// <summary>Estado atual da ligação ao servidor (para o indicador na barra de estado).</summary>
    public ServerConnectionState State { get; private set; } = ServerConnectionState.Offline;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        SetState(ServerConnectionState.Offline);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var baseUrl = _baseUrlProvider()?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                SetState(ServerConnectionState.Offline);
                await DelayAsync(TimeSpan.FromSeconds(5), ct);
                continue;
            }

            try
            {
                SetState(ServerConnectionState.Connecting);
                await using var stream = await _http.GetStreamAsync($"{baseUrl}/events", ct);
                using var reader = new StreamReader(stream);
                SetState(ServerConnectionState.Connected);

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break; // servidor fechou a ligação — reconecta
                    if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                    if (!long.TryParse(line.AsSpan(5).Trim(), out var version)) continue;

                    // Primeira versão recebida = baseline (não dispara). Depois, qualquer
                    // diferença significa que o conteúdo mudou (ou o servidor reiniciou).
                    if (_lastVersion >= 0 && version != _lastVersion) Raise();
                    _lastVersion = version;
                }
            }
            catch (OperationCanceledException)
            {
                return; // Stop() foi chamado
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Stream de eventos do servidor caiu — a reconectar");
            }

            SetState(ServerConnectionState.Offline);
            await DelayAsync(TimeSpan.FromSeconds(5), ct);
        }
    }

    private void Raise()
    {
        if (_sync is not null)
            _sync.Post(_ => ContentChanged?.Invoke(), null);
        else
            ContentChanged?.Invoke();
    }

    private void SetState(ServerConnectionState state)
    {
        if (State == state) return;
        State = state;
        if (_sync is not null)
            _sync.Post(_ => ConnectionChanged?.Invoke(), null);
        else
            ConnectionChanged?.Invoke();
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try { await Task.Delay(delay, ct); }
        catch (OperationCanceledException) { /* a terminar */ }
    }
}
