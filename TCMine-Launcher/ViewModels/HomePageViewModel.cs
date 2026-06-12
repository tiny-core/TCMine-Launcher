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
///     Página "Jogar": hero do modpack activo + painel de perfil + launch.
///     O launch é real: instala NeoForge e arranca o jogo via <see cref="GameLauncher" />.
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

    [ObservableProperty] private ObservableCollection<string> _minecraftVersions;

    [ObservableProperty] private ObservableCollection<string> _neoForgeVersions;

    [ObservableProperty] private string? _selectedMinecraftVersion;

    [ObservableProperty] private string? _selectedNeoForgeVersion;

    public HomePageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        ActiveModpack = new Modpack
        {
            Name = "TCMine Modpack",
            Author = "Você",
            Version = "1.0.0",
            Tagline = "MODPACK OFICIAL",
            Description = "O pack de mods custom do servidor TCMine — exploração, " +
                          "tecnologia e aventura numa só experiência.",
            IsInstalled = false
        };

        _minecraftVersions = new ObservableCollection<string>
        {
            "1.21.4", "1.21.3", "1.21.1", "1.20.6", "1.20.4", "1.20.1"
        };
        _neoForgeVersions = new ObservableCollection<string>
        {
            "21.1.172", "21.1.171", "21.1.170", "21.1.165", "21.1.160"
        };
        _selectedMinecraftVersion = _game.MinecraftVersion;
        _selectedNeoForgeVersion = _game.NeoForgeVersion;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    public partial bool IsLaunching { get; set; }

    public Modpack ActiveModpack { get; }

    /// <summary>Linhas do console de launch (registo).</summary>
    public ObservableCollection<string> LaunchLog { get; } = new();

    public string LogToggleLabel => IsLogExpanded ? "▾ Ocultar registo" : "▸ Mostrar registo";

    // ── Perfil (delegado ao Model) ───────────────────────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    public string RamDisplay => $"{_game.AllocatedRamMb} MB";

    /// <summary>Chamado pelo shell quando o login muda o perfil.</summary>
    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
    }

    partial void OnSelectedMinecraftVersionChanged(string? value)
    {
        if (value is null) return;
        _game.MinecraftVersion = value;
        _shell.PersistSettings();
    }

    partial void OnSelectedNeoForgeVersionChanged(string? value)
    {
        if (value is null) return;
        _game.NeoForgeVersion = value;
        _shell.PersistSettings();
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

        IsLaunching = true;
        LaunchProgress = 0;
        LaunchLog.Clear();
        LaunchLog.Add($"Modpack: {ActiveModpack.Name} ({ActiveModpack.VersionSummary})");
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
                LauncherPaths.DefaultGameDir,
                _game.MinecraftVersion,
                _game.NeoForgeVersion,
                session,
                _game.AllocatedRamMb,
                _game.JavaPath,
                progress,
                _launchCts.Token);

            process.Start();
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
        }
    }

    [RelayCommand]
    private void CancelLaunch()
    {
        _launchCts?.Cancel();
    }
}