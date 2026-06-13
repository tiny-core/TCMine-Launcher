using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Jogar": hero da instância ativa + painel de perfil + launch real.
///     O botão alterna entre <b>Instalar</b> (sem ficheiros), <b>Jogar</b> (instalada)
///     e fica desativado enquanto o jogo está aberto. Deteta o fecho do processo.
/// </summary>
public partial class HomePageViewModel : ViewModelBase
{
    private readonly GameProfile _game;
    private readonly GameLauncher _launcher = new();
    private readonly PlayerProfile _player;
    private readonly MainWindowViewModel _shell;

    /// <summary>Permite cancelar uma instalação/launch em curso.</summary>
    private CancellationTokenSource? _launchCts;

    /// <summary>Evita persistir a RAM ao trocar de instância (só na interação do utilizador).</summary>
    private bool _suppressRam;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LogToggleLabel))]
    private bool _isLogExpanded;

    [ObservableProperty] private double _launchProgress;

    [ObservableProperty] private string _launchStatus = "Pronto para jogar";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RamDisplay))]
    private double _instanceRam = 4096;

    public HomePageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        _instanceRam = Active?.RamOverrideMb ?? _game.AllocatedRamMb;
        LaunchStatus = DefaultStatus();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    public partial bool IsLaunching { get; set; }

    /// <summary>Instância ativa (a que será lançada). Vem do shell.</summary>
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

    /// <summary>Linhas do console de launch (registo).</summary>
    public ObservableCollection<string> LaunchLog { get; } = new();

    public string LogToggleLabel => IsLogExpanded ? "▾ Ocultar registo" : "▸ Mostrar registo";

    // ── Perfil (delegado ao Model) ───────────────────────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    /// <summary>UUID abreviado do jogador (info extra no painel).</summary>
    public string PlayerUuidShort => string.IsNullOrEmpty(_player.Uuid)
        ? "—"
        : _player.Uuid.Length > 12 ? _player.Uuid[..12] + "…" : _player.Uuid;

    /// <summary>RAM efetiva mostrada junto ao slider.</summary>
    public string RamDisplay => $"{(int)InstanceRam} MB";

    /// <summary>Chamado pelo shell quando o login muda o perfil.</summary>
    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(PlayerUuidShort));
    }

    /// <summary>Chamado pelo shell quando a instância ativa muda.</summary>
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
    }

    /// <summary>Chamado pelo shell quando o jogo abre/fecha — atualiza botão e estado.</summary>
    public void NotifyGameRunningChanged()
    {
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayIcon));
        OnPropertyChanged(nameof(StateLabel));
        PlayCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Reavalia (a partir do disco) se a instância ativa está instalada.</summary>
    private void RefreshInstallState()
    {
        OnPropertyChanged(nameof(IsInstalled));
        OnPropertyChanged(nameof(PlayLabel));
        OnPropertyChanged(nameof(PlayIcon));
        OnPropertyChanged(nameof(StateLabel));
    }

    private string DefaultStatus()
    {
        if (_shell.IsGameRunning) return "Minecraft em execução";
        return IsInstalled ? "Pronto para jogar" : "Instância por instalar";
    }

    partial void OnInstanceRamChanged(double value)
    {
        if (_suppressRam || Active is null) return;
        Active.RamOverrideMb = (int)value;
        _shell.SaveInstance(Active);
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

        // Recebe o progresso do GameLauncher (thread de fundo) e reflete-o na UI.
        var progress = new Progress<LaunchProgress>(p =>
        {
            LaunchProgress = p.Percent;
            LaunchStatus = p.Message;
            LaunchLog.Add($"[{p.Percent,3:0}%] {p.Message}");
            _shell.SetBusy(p.IsActive, p.Percent, p.Message);
        });

        try
        {
            // Instâncias com servidor: escreve o servers.dat e entra direto no 1º.
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

            // Descarrega os mods em falta (CurseForge) antes de arrancar.
            await _shell.ModInstaller.EnsureModsAsync(instance, progress, _launchCts.Token);

            process.Start();
            instance.LastPlayedAt = DateTimeOffset.Now;
            _shell.SaveInstance(instance);

            _shell.IsGameRunning = true;
            LaunchStatus = "Minecraft em execução";
            LaunchLog.Add("Minecraft iniciado.");

            // Deteta o fecho do jogo para reativar os botões (resume na UI thread).
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
            RefreshInstallState(); // após instalar, o botão passa a "Jogar"
        }
    }

    /// <summary>Aguarda o fecho do Minecraft e repõe o estado da UI.</summary>
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
