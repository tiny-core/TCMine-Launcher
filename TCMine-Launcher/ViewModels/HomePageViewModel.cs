using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Jogar": hero do modpack activo + painel de perfil + launch.
///     O launch é apenas uma simulação visual (sem CmlLib por agora).
/// </summary>
public partial class HomePageViewModel : ViewModelBase
{
    private readonly GameProfile _game;
    private readonly PlayerProfile _player;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
    private bool _isLaunching;

    [ObservableProperty] private double _launchProgress;

    [ObservableProperty] private string _launchStatus = "Pronto para jogar";

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LogToggleLabel))]
    private bool _isLogExpanded;

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
        if (value is not null) _game.MinecraftVersion = value;
    }

    partial void OnSelectedNeoForgeVersionChanged(string? value)
    {
        if (value is not null) _game.NeoForgeVersion = value;
    }

    private bool CanPlay()
    {
        return !IsLaunching;
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task PlayAsync()
    {
        // TODO: substituir pela integração real com CmlLib (download + launch).
        IsLaunching = true;
        LaunchProgress = 0;
        LaunchLog.Clear();
        LaunchLog.Add($"Modpack: {ActiveModpack.Name} ({ActiveModpack.VersionSummary})");

        var steps = new (int Pct, string Msg)[]
        {
            (12, "A verificar ficheiros..."),
            (34, "A descarregar assets..."),
            (58, "A instalar NeoForge..."),
            (80, "A preparar JVM..."),
            (100, "A iniciar Minecraft...")
        };

        foreach (var (pct, msg) in steps)
        {
            await Task.Delay(650);
            LaunchProgress = pct;
            LaunchStatus = msg;
            LaunchLog.Add($"[{pct,3}%] {msg}");
            _shell.SetBusy(true, pct, msg);
        }

        await Task.Delay(700);
        LaunchLog.Add("[100%] Minecraft iniciado.");
        LaunchStatus = "Pronto para jogar";
        LaunchProgress = 0;
        IsLaunching = false;
        _shell.SetBusy(false, 0, "Pronto");
    }
}
