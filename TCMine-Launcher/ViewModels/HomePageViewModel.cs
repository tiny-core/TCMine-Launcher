using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Jogar": hero da instância ativa + painel de perfil + launch real.
///     O botão alterna entre <b>Instalar</b> (sem ficheiros) e <b>Jogar</b> (instalada).
///     Instala NeoForge e arranca o jogo na pasta isolada da instância.
/// </summary>
public partial class HomePageViewModel : ViewModelBase
{
    private readonly GameProfile _game;
    private readonly GameLauncher _launcher = new();
    private readonly PlayerProfile _player;
    private readonly MainWindowViewModel _shell;

    /// <summary>Permite cancelar uma instalação/launch em curso.</summary>
    private CancellationTokenSource? _launchCts;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LogToggleLabel))]
    private bool _isLogExpanded;

    [ObservableProperty] private double _launchProgress;

    [ObservableProperty] private string _launchStatus = "Pronto para jogar";

    public HomePageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

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

    // ── Estado de instalação (define Instalar vs Jogar) ──────────
    public bool IsInstalled => Active is not null && _shell.IsInstanceInstalled(Active);
    public string PlayLabel => IsInstalled ? "JOGAR" : "INSTALAR";
    public string PlayIcon => IsInstalled ? "▶" : "⬇";
    public string StateLabel => IsInstalled ? "Instalada" : "Por instalar";

    /// <summary>Linhas do console de launch (registo).</summary>
    public ObservableCollection<string> LaunchLog { get; } = new();

    public string LogToggleLabel => IsLogExpanded ? "▾ Ocultar registo" : "▸ Mostrar registo";

    // ── Perfil (delegado ao Model) ───────────────────────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    /// <summary>RAM efetiva: override da instância ou default global.</summary>
    public string RamDisplay => $"{Active?.RamOverrideMb ?? _game.AllocatedRamMb} MB";

    /// <summary>Chamado pelo shell quando o login muda o perfil.</summary>
    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
    }

    /// <summary>Chamado pelo shell quando a instância ativa muda.</summary>
    public void NotifyInstanceChanged()
    {
        OnPropertyChanged(nameof(InstanceName));
        OnPropertyChanged(nameof(InstanceTag));
        OnPropertyChanged(nameof(InstanceSubtitle));
        OnPropertyChanged(nameof(InstanceSummary));
        OnPropertyChanged(nameof(RamDisplay));
        RefreshInstallState();

        if (!IsLaunching) LaunchStatus = DefaultStatus();
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
        return IsInstalled ? "Pronto para jogar" : "Instância por instalar";
    }

    private bool CanPlay()
    {
        return !IsLaunching;
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
            var process = await _launcher.PrepareAsync(
                LauncherPaths.InstanceGameDir(instance.Id),
                instance.MinecraftVersion,
                instance.NeoForgeVersion,
                session,
                instance.RamOverrideMb ?? _game.AllocatedRamMb,
                _game.JavaPath,
                progress,
                _launchCts.Token);

            process.Start();
            instance.LastPlayedAt = DateTimeOffset.Now;
            _shell.SaveInstance(instance);

            LaunchLog.Add("Minecraft iniciado.");
            LaunchStatus = "Minecraft em execução";
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

    [RelayCommand]
    private void CancelLaunch()
    {
        _launchCts?.Cancel();
    }
}
