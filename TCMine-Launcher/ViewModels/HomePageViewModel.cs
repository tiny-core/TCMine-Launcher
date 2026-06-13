using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Jogar": hero da instância ativa + perfil + launch real. O indicador
///     de estado muda de cor conforme o estado (por instalar / pronto / a instalar /
///     em execução) e, em instâncias com servidor, mostra o estado online/offline.
/// </summary>
public partial class HomePageViewModel : ViewModelBase
{
    private readonly GameProfile _game;
    private readonly GameLauncher _launcher = new();
    private readonly MinecraftServerPinger _pinger = new();
    private readonly PlayerProfile _player;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RamDisplay))] [NotifyPropertyChangedFor(nameof(RamMbDecimal))]
    private double _instanceRam = 4096;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LogToggleLabel))]
    private bool _isLogExpanded;

    private CancellationTokenSource? _launchCts;

    [ObservableProperty] private double _launchProgress;
    [ObservableProperty] private string _launchStatus = "Pronto para jogar";
    private bool _suppressRam;

    public HomePageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        _instanceRam = Active?.RamOverrideMb ?? _game.AllocatedRamMb;
        LaunchStatus = DefaultStatus();

        RebuildServers();
        _ = ServerStatusLoopAsync();
    }

    /// <summary>RAM em MB para o campo numérico editável (NumericUpDown usa decimal).</summary>
    public decimal? RamMbDecimal
    {
        get => (decimal)InstanceRam;
        set
        {
            if (value is decimal mb) InstanceRam = (double)mb;
        }
    }

    /// <summary>Estado (online/offline + jogadores + MOTD) de cada servidor do modpack.</summary>
    public ObservableCollection<ServerStatusItem> Servers { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    public partial bool IsLaunching { get; set; }

    private MinecraftInstance? Active => _shell.ActiveInstance;

    // ── Hero (derivado da instância ativa) ───────────────────────
    public string InstanceName => Active?.Name ?? "—";
    public string InstanceTag => Active?.SourceLabel ?? "";

    public string InstanceSubtitle => Active is { IsOfficial: true }
        ? "Modpack oficial do servidor TCMine"
        : "Instância personalizada";

    public string InstanceSummary => Active?.VersionSummary ?? "";

    // ── Estado de instalação / execução ──────────────────────────
    public bool IsInstalled => Active is not null && _shell.IsInstanceInstalled(Active);

    public string PlayLabel => _shell.IsGameRunning
        ? "EM EXECUÇÃO"
        : IsInstalled
            ? "JOGAR"
            : "INSTALAR";

    public string PlayIcon => _shell.IsGameRunning ? "■" : IsInstalled ? "▶" : "⬇";

    public string StateLabel => _shell.IsGameRunning
        ? "Em execução"
        : IsInstalled
            ? "Instalada"
            : "Por instalar";

    /// <summary>Cor do indicador de estado (verde/âmbar/azul/roxo).</summary>
    public string StatusColor =>
        _shell.IsGameRunning ? "#3B82F6" :
        IsLaunching ? "#A855F7" :
        IsInstalled ? "#22C55E" :
        "#F59E0B";

    // ── Servidor da instância ────────────────────────────────────
    public bool HasServer => Servers.Count > 0;

    public ObservableCollection<string> LaunchLog { get; } = new();
    public string LogToggleLabel => IsLogExpanded ? "▾ Ocultar registo" : "▸ Mostrar registo";

    // ── Perfil ───────────────────────────────────────────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    public string PlayerUuidShort => string.IsNullOrEmpty(_player.Uuid)
        ? "—"
        : _player.Uuid.Length > 12
            ? _player.Uuid[..12] + "…"
            : _player.Uuid;

    public string PlayerUuid => string.IsNullOrEmpty(_player.Uuid)
        ? "_"
        : _player.Uuid;

    /// <summary>URL da cabeça da skin (para o avatar).</summary>
    public string? PlayerHeadUrl => _player.HeadUrl;

    public string RamDisplay => $"{(int)InstanceRam} MB";

    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(PlayerUuidShort));
        OnPropertyChanged(nameof(PlayerUuid));
        OnPropertyChanged(nameof(PlayerHeadUrl));
    }

    public void NotifyInstanceChanged()
    {
        _suppressRam = true;
        InstanceRam = Active?.RamOverrideMb ?? _game.AllocatedRamMb;
        _suppressRam = false;

        OnPropertyChanged(nameof(InstanceName));
        OnPropertyChanged(nameof(InstanceTag));
        OnPropertyChanged(nameof(InstanceSubtitle));
        OnPropertyChanged(nameof(InstanceSummary));
        RefreshInstallState();

        if (!IsLaunching) LaunchStatus = DefaultStatus();

        RebuildServers();
        _ = RefreshServersAsync();
    }

    public void NotifyGameRunningChanged()
    {
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayIcon));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StatusColor));
        PlayCommand.NotifyCanExecuteChanged();
    }

    private void RefreshInstallState()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayIcon));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StatusColor));
    }

    private string DefaultStatus()
    {
        if (_shell.IsGameRunning) return "Minecraft em execução";
        return IsInstalled ? "Pronto para jogar" : "Instância por instalar";
    }

    // ── Ping periódico dos servidores ────────────────────────────
    private void RebuildServers()
    {
        Servers.Clear();
        if (Active is not null)
            foreach (var server in Active.Servers)
                Servers.Add(new ServerStatusItem(server));
        OnPropertyChanged(nameof(HasServer));
    }

    private async Task ServerStatusLoopAsync()
    {
        while (true)
        {
            await RefreshServersAsync();
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private async Task RefreshServersAsync()
    {
        foreach (var item in Servers.ToList())
        {
            var status = await _pinger.PingAsync(item.Server.Address, item.Server.Port);

            // O item pode ter saído da lista durante o ping (troca de instância).
            if (!Servers.Contains(item)) continue;

            if (status.Online)
            {
                item.StatusText = $"Online · {status.PlayersOnline}/{status.PlayersMax}";
                item.StatusColor = "#22C55E";
                item.Motd = status.Motd;
            }
            else
            {
                item.StatusText = "Offline";
                item.StatusColor = "#EF4444";
                item.Motd = string.Empty;
            }
        }
    }

    private CancellationTokenSource? _ramSaveCts;

    partial void OnInstanceRamChanged(double value)
    {
        if (_suppressRam || Active is null) return;
        Active.RamOverrideMb = (int)value;
        DebounceSaveRam(Active); // evita gravar a cada "tick" do slider
    }

    private void DebounceSaveRam(MinecraftInstance instance)
    {
        _ramSaveCts?.Cancel();
        _ramSaveCts = new CancellationTokenSource();
        var ct = _ramSaveCts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(600, ct); }
            catch { return; }
            if (!ct.IsCancellationRequested) _shell.SaveInstance(instance);
        }, ct);
    }

    private bool CanPlay()
    {
        return !IsLaunching && !_shell.IsGameRunning;
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        var session = _shell.CurrentSession;
        if (session is null)
        {
            LaunchStatus = "Sessão inválida — faz login novamente.";
            return;
        }

        var instance = Active;
        if (instance is null)
        {
            LaunchStatus = "Nenhuma instância selecionada.";
            return;
        }

        IsLaunching = true;
        LaunchProgress = 0;
        LaunchLog.Clear();
        LaunchLog.Add($"Instância: {instance.Name} ({instance.VersionSummary})");
        _launchCts = new CancellationTokenSource();

        var progress = new Progress<LaunchProgress>(p =>
        {
            LaunchProgress = p.Percent;
            LaunchStatus = p.Message;
            LaunchLog.Add($"[{p.Percent,3:0}%] {p.Message}");
            _shell.SetBusy(p.IsActive, p.Percent, p.Message);
        });

        try
        {
            var autoJoin = instance.Servers.Count > 0 ? instance.Servers[0] : null;

            var process = await _launcher.PrepareAsync(
                LauncherPaths.InstanceGameDir(instance.Id),
                instance.MinecraftVersion,
                instance.NeoForgeVersion,
                session,
                instance.RamOverrideMb ?? _game.AllocatedRamMb,
                string.IsNullOrWhiteSpace(instance.JavaPathOverride) ? _game.JavaPath : instance.JavaPathOverride,
                progress,
                _launchCts.Token,
                instance.Servers,
                autoJoin);

            await _shell.ModInstaller.EnsureModsAsync(instance, progress, _launchCts.Token);

            // Captura a saída do jogo para ficheiro (e deteção de crash).
            var logCapture = new GameLogCapture(LauncherPaths.InstanceLogFile(instance.Id));
            process.OutputDataReceived += (_, e) => logCapture.Append(e.Data);
            process.ErrorDataReceived += (_, e) => logCapture.Append(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            instance.LastPlayedAt = DateTimeOffset.Now;
            _shell.SaveInstance(instance);

            _shell.IsGameRunning = true;
            LaunchStatus = "Minecraft em execução";
            LaunchLog.Add("Minecraft iniciado.");

            _ = MonitorGameAsync(process, logCapture);
        }
        catch (OperationCanceledException)
        {
            LaunchStatus = "Launch cancelado";
            LaunchLog.Add("Launch cancelado pelo utilizador.");
        }
        catch (Exception ex)
        {
            LaunchStatus = "Falha no launch";
            LaunchLog.Add("ERRO: " + ex.Message);
        }
        finally
        {
            _launchCts?.Dispose();
            _launchCts = null;
            LaunchProgress = 0;
            IsLaunching = false;
            _shell.SetBusy(false, 0, "Pronto");
            RefreshInstallState();
        }
    }

    private async Task MonitorGameAsync(Process process, GameLogCapture logCapture)
    {
        var exitCode = 0;
        try
        {
            await process.WaitForExitAsync();
            exitCode = process.ExitCode;
        }
        catch
        {
            // ignora — o importante é reativar a UI a seguir
        }

        _shell.IsGameRunning = false;

        if (exitCode != 0)
        {
            LaunchStatus = "Minecraft terminou com erro";
            LaunchLog.Add($"⚠ Saída com código {exitCode}. Log: {logCapture.LogPath}");
            foreach (var line in logCapture.Tail()) LaunchLog.Add(line);
            IsLogExpanded = true;
        }
        else
        {
            LaunchLog.Add("Minecraft fechado.");
            LaunchStatus = DefaultStatus();
        }

        RefreshInstallState();
        logCapture.Dispose();

        try { process.Dispose(); }
        catch { /* noop */ }
    }

    [RelayCommand]
    private void CancelLaunch()
    {
        _launchCts?.Cancel();
    }
}

/// <summary>Estado de um servidor do modpack (para a lista na tela principal).</summary>
public partial class ServerStatusItem : ViewModelBase
{
    [ObservableProperty] private string _motd = string.Empty;
    [ObservableProperty] private string _statusColor = "#6B7280";
    [ObservableProperty] private string _statusText = "A verificar...";

    public ServerStatusItem(ServerEntry server)
    {
        Server = server;
        Name = server.Name;
    }

    public ServerEntry Server { get; }
    public string Name { get; }
}