using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>Abas de navegação da aplicação.</summary>
public enum AppTab
{
    Home,
    Modpacks,
    News,
    Settings
}

/// <summary>
///     ViewModel raiz (shell). RESPONSABILIDADES:
///     ✓ Gere a autenticação (mostrar Login vs aplicação)
///     ✓ Mantém o estado de navegação (qual aba/página está activa)
///     ✓ Cria e guarda as ViewModels de cada página
///     ✓ Partilha os Models (PlayerProfile, GameProfile) com as páginas
///     NÃO FAZ lógica real de auth/download — só UI/navegação por agora.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // Models partilhados (dados puros, sem UI)
    private readonly GameProfile _game;
    private readonly PlayerProfile _player;

    // ── Estado de autenticação / navegação ───────────────────────
    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty] private bool _isLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsModpacksSelected))]
    [NotifyPropertyChangedFor(nameof(IsNewsSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private AppTab _selectedTab = AppTab.Home;

    [ObservableProperty] private string _statusMessage = "Não autenticado";

    public MainWindowViewModel()
    {
        _player = new PlayerProfile();
        _game = new GameProfile();

        Home = new HomePageViewModel(_player, _game);
        Modpacks = new ModpacksPageViewModel();
        News = new NewsPageViewModel();
        Settings = new SettingsPageViewModel(_player, _game, this);

        _currentPage = Home;
    }

    // ── Páginas (criadas uma vez, reutilizadas) ──────────────────
    public HomePageViewModel Home { get; }
    public ModpacksPageViewModel Modpacks { get; }
    public NewsPageViewModel News { get; }
    public SettingsPageViewModel Settings { get; }

    // ── Perfil do jogador (para o chip da sidebar) ───────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    // ── Realce da aba activa (bind via Classes.active) ───────────
    public bool IsHomeSelected => SelectedTab == AppTab.Home;
    public bool IsModpacksSelected => SelectedTab == AppTab.Modpacks;
    public bool IsNewsSelected => SelectedTab == AppTab.News;
    public bool IsSettingsSelected => SelectedTab == AppTab.Settings;

    public string PageTitle => SelectedTab switch
    {
        AppTab.Modpacks => "Modpacks",
        AppTab.News => "Novidades",
        AppTab.Settings => "Definições",
        _ => "Jogar"
    };

    partial void OnSelectedTabChanged(AppTab value)
    {
        CurrentPage = value switch
        {
            AppTab.Modpacks => Modpacks,
            AppTab.News => News,
            AppTab.Settings => Settings,
            _ => Home
        };
    }

    // ── Comandos de navegação ────────────────────────────────────
    [RelayCommand]
    private void Navigate(AppTab tab)
    {
        SelectedTab = tab;
    }

    // ── Autenticação (placeholder — sem lógica real) ─────────────
    [RelayCommand]
    private void LoginMicrosoft()
    {
        // TODO: integrar XboxAuthNet/MSAL para obter a sessão real.
        _player.Name = "Steve";
        _player.AccountType = AccountType.Microsoft;
        EnterApp("Pronto");
    }

    [RelayCommand]
    private void PlayOffline()
    {
        _player.AccountType = AccountType.Offline;
        EnterApp("Pronto · modo offline");
    }

    [RelayCommand]
    private void Logout()
    {
        IsLoggedIn = false;
        StatusMessage = "Não autenticado";
    }

    private void EnterApp(string status)
    {
        RefreshPlayer();
        SelectedTab = AppTab.Home;
        IsLoggedIn = true;
        StatusMessage = status;
    }

    /// <summary>Notifica todas as views que dependem do perfil do jogador.</summary>
    private void RefreshPlayer()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        Home.NotifyPlayerChanged();
        Settings.NotifyPlayerChanged();
    }
}
