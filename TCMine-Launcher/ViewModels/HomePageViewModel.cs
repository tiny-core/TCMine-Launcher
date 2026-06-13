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
    private readonly PlayerProfile _player;
    private readonly ServerStatusService _serverStatus = new();
    private readonly MainWindowViewModel _shell;

    private CancellationTokenSource? _launchCts;
    private bool _suppressRam;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LogToggleLabel))]
    private bool _isLogExpanded;

    [ObservableProperty] private double _launchProgress;
    [ObservableProperty] private string _launchStatus = "Pronto para jogar";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RamDisplay))]
    private double _instanceRam = 4096;

    // ── Estado do servidor (instâncias com servidor) ─────────────
    [ObservableProperty] private string _serverName = string.Empty;
    [ObservableProperty] private string _serverStatusText = string.Empty;
    [ObservableProperty] private string _serverStatusColor = "#6B7280";

    public HomePageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        _instanceRam = Active?.RamOverrideMb ?? _game.AllocatedRamMb;
        LaunchStatus = DefaultStatus();

        _ = ServerStatusLoopAsync();
    }

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
        : IsInstalled ? "JOGAR" : "INSTALAR";

    public string PlayIcon => _shell.IsGameRunning ? "■" : IsInstalled ? "▶" : "⬇";

    public string StateLabel => _shell.IsGameRunning
        ? "Em execução"
        : IsInstalled ? "Instalada" : "Por instalar";

    /// <summary>Cor do indicador de estado (verde/âmbar/azul/roxo).</summary>
    public string StatusColor =>
        _shell.IsGameRunning ? "#3B82F6" :
        IsLaunching ? "#A855F7" :
        IsInstalled ? "#22C55E" :
        "#F59E0B";

    // ── Servidor da instância ────────────────────────────────────
    public bool HasServer => Active is { Servers.Count: > 0 };

    public ObservableCollection<string> LaunchLog { get; } = new();
    public string LogToggleLabel => IsLogExpanded ? "▾ Ocultar registo" : "▸ Mostrar registo";

    // ── Perfil ───────────────────────────────────────────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    public string PlayerUuidShort => string.IsNullOrEmpty(_player.Uuid)
        ? "—"
        : _player.Uuid.Length > 12 ? _player.Uuid[..12] + "…" : _player.Uuid;

    public string RamDisplay => $"{(int)InstanceRam} MB";

    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(PlayerUuidShort));
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
        OnPropertyChanged(nameof(HasServer));
        RefreshInstallState();

        if (!IsLaunching) LaunchStatus = DefaultStatus();

        _ = RefreshServerStatusAsync();
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

    // ── Ping periódico do servidor ───────────────────────────────
    private async Task ServerStatusLoopAsync()
    {
        while (true)
        {
            await RefreshServerStatusAsync();
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
    }

    private async Task RefreshServerStatusAsync()
    {
        var server = Active?.Servers.FirstOrDefault();
        if (server is null)
        {
            ServerName = string.Empty;
            ServerStatusText = string.Empty;
            ServerStatusColor = "#6B7280";
            return;
        }

        ServerName = server.Name;
        ServerStatusText = "A verificar...";
        ServerStatusColor = "#6B7280";

        var online = await _serverStatus.IsOnlineAsync(server.Address, server.Port);

        // A instância pode ter mudado durante o ping.
        if (Active?.Servers.FirstOrDefault() != server) return;

        ServerStatusText = online ? "Online" : "Offline";
        ServerStatusColor = online ? "#22C55E" : "#EF4444";
    }

    partial void OnInstanceRamChanged(double value)
    {
        if (_suppressRam || Active is null) return;
        Active.RamOverrideMb = (int)value;
        _shell.SaveInstance(Active);
    }

    private bool CanPlay() => !IsLaunching && !_shell.IsGameRunning;

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
                _game.JavaPath,
                progress,
                _launchCts.Token,
                instance.Servers,
                autoJoin);

            await _shell.ModInstaller.EnsureModsAsync(instance, progress, _launchCts.Token);

            process.Start();
            instance.LastPlayedAt = DateTimeOffset.Now;
            _shell.SaveInstance(instance);

            _shell.IsGameRunning = true;
            LaunchStatus = "Minecraft em execução";
            LaunchLog.Add("Minecraft iniciado.");

            _ = MonitorGameAsync(process);
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

    private async Task MonitorGameAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch
        {
            // ignora — o importante é reativar a UI a seguir
        }

        _shell.IsGameRunning = false;
        LaunchLog.Add("Minecraft fechado.");
        LaunchStatus = DefaultStatus();
        RefreshInstallState();

        try { process.Dispose(); }
        catch { /* noop */ }
    }

    [RelayCommand]
    private void CancelLaunch()
    {
        _launchCts?.Cancel();
    }
}
