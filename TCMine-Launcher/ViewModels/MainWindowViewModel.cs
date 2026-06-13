using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>Abas de navegação da aplicação.</summary>
public enum AppTab
{
    Home,
    Instances,
    Modpacks,
    News,
    Settings
}

/// <summary>
///     ViewModel raiz (shell). RESPONSABILIDADES:
///     ✓ Mantém o estado de navegação (qual aba/página está activa)
///     ✓ Cria e guarda as ViewModels de cada página
///     ✓ Partilha os Models (PlayerProfile, GameProfile) com as páginas
///     ✓ Encaminha pedidos da UI (diálogos, pickers, janelas) para a View
///     A autenticação está em <c>MainWindowViewModel.Auth.cs</c> e a gestão de
///     instâncias em <c>MainWindowViewModel.Instances.cs</c> (classes parciais).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // Models partilhados (dados puros, sem UI)
    private readonly GameProfile _game;
    private readonly PlayerProfile _player;

    // Serviços
    private readonly SettingsService _settings = new();

    /// <summary>Cliente CurseForge (via proxy), instalador de mods e manifestos.</summary>
    public CurseForgeClient CurseForge { get; }
    public ModInstaller ModInstaller { get; }
    public ManifestService Manifest { get; }
    public NewsService NewsFeed { get; }
    private readonly AppUpdater _updater;

    // ── Auto-update do launcher ──────────────────────────────────
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _latestVersion = string.Empty;

    // ── Estado de autenticação / navegação ───────────────────────
    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty] private bool _isLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsInstancesSelected))]
    [NotifyPropertyChangedFor(nameof(IsModpacksSelected))]
    [NotifyPropertyChangedFor(nameof(IsNewsSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private AppTab _selectedTab = AppTab.Home;

    [ObservableProperty] private string _statusMessage = "Não autenticado";

    // ── Estado da autenticação ───────────────────────────────────
    [ObservableProperty] private bool _isAuthenticating;

    [ObservableProperty] private string? _loginError;

    // ── Progresso global (status bar) ────────────────────────────
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private double _globalProgress;

    /// <summary>True enquanto há um Minecraft aberto — desativa ações nas páginas.</summary>
    [ObservableProperty] private bool _isGameRunning;

    partial void OnIsGameRunningChanged(bool value)
    {
        Home.NotifyGameRunningChanged();
        InstancesPage.NotifyGameRunningChanged();
    }

    public MainWindowViewModel()
    {
        _player = new PlayerProfile();
        _game = _settings.Load();

        CurseForge = new CurseForgeClient(() => _game.ServerUrl);
        ModInstaller = new ModInstaller(CurseForge);
        Manifest = new ManifestService(() => _game.ServerUrl);
        NewsFeed = new NewsService(() => _game.ServerUrl);
        _updater = new AppUpdater(() => _game.ServerUrl);

        LoadInstances();

        Home = new HomePageViewModel(_player, _game, this);
        InstancesPage = new InstancesPageViewModel(this);
        CreateInstancePage = new CreateInstancePageViewModel(_game, this);
        InstanceModsPage = new InstanceModsPageViewModel(this);
        Modpacks = new ModpacksPageViewModel(this, Manifest);
        News = new NewsPageViewModel(NewsFeed);
        Settings = new SettingsPageViewModel(_player, _game, this);

        _currentPage = Home;

        // Tenta entrar automaticamente se houver uma sessão em cache.
        _ = TrySilentLoginAsync();

        // Verifica se há uma versão mais recente do launcher.
        _ = CheckForUpdateAsync();
    }

    /// <summary>Versão atual do launcher (assembly).</summary>
    public string CurrentVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private async Task CheckForUpdateAsync()
    {
        if (await _updater.CheckAsync())
        {
            LatestVersion = _updater.LatestVersion ?? string.Empty;
            UpdateAvailable = true;
            Log.Information("Atualização disponível: {Version}", LatestVersion);
        }
    }

    [RelayCommand]
    private async Task OpenUpdate()
    {
        if (!UpdateAvailable) return;
        StatusMessage = "A atualizar o launcher...";
        await _updater.ApplyAndRestartAsync();
    }

    // ── Páginas (criadas uma vez, reutilizadas) ──────────────────
    public HomePageViewModel Home { get; }
    public InstancesPageViewModel InstancesPage { get; }
    public CreateInstancePageViewModel CreateInstancePage { get; }
    public InstanceModsPageViewModel InstanceModsPage { get; }
    public ModpacksPageViewModel Modpacks { get; }
    public NewsPageViewModel News { get; }
    public SettingsPageViewModel Settings { get; }

    // ── Perfil do jogador (para o chip da sidebar) ───────────────
    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;
    public string? PlayerHeadUrl => _player.HeadUrl;

    /// <summary>Notifica todas as views que dependem do perfil do jogador.</summary>
    private void RefreshPlayer()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(PlayerHeadUrl));
        Home.NotifyPlayerChanged();
        Settings.NotifyPlayerChanged();
    }

    // ── Realce da aba activa (bind via Classes.active) ───────────
    public bool IsHomeSelected => SelectedTab == AppTab.Home;
    public bool IsInstancesSelected => SelectedTab == AppTab.Instances;
    public bool IsModpacksSelected => SelectedTab == AppTab.Modpacks;
    public bool IsNewsSelected => SelectedTab == AppTab.News;
    public bool IsSettingsSelected => SelectedTab == AppTab.Settings;

    public string PageTitle => SelectedTab switch
    {
        AppTab.Instances => "Instâncias",
        AppTab.Modpacks => "Modpacks",
        AppTab.News => "Novidades",
        AppTab.Settings => "Definições",
        _ => "Jogar"
    };

    partial void OnSelectedTabChanged(AppTab value)
    {
        CurrentPage = value switch
        {
            AppTab.Instances => InstancesPage,
            AppTab.Modpacks => Modpacks,
            AppTab.News => News,
            AppTab.Settings => Settings,
            _ => Home
        };

        if (value == AppTab.Modpacks) Modpacks.Begin();
        if (value == AppTab.News) News.Begin();
    }

    // ── Comandos / atalhos de navegação ──────────────────────────
    [RelayCommand]
    private void Navigate(AppTab tab)
    {
        SelectedTab = tab;
    }

    /// <summary>Atalho para voltar à página "Jogar".</summary>
    public void NavigateToHome()
    {
        SelectedTab = AppTab.Home;
    }

    /// <summary>Abre a página dedicada de criação de instância (mantém a aba Instâncias ativa).</summary>
    public void ShowCreateInstance()
    {
        CreateInstancePage.Begin();
        CurrentPage = CreateInstancePage;
    }

    /// <summary>Volta das páginas de criação/mods para a lista de instâncias.</summary>
    public void BackToInstances()
    {
        SelectedTab = AppTab.Instances;
        CurrentPage = InstancesPage;
    }

    // ── Pedidos para a camada View (janelas / diálogos / pickers) ─

    /// <summary>Pedido para abrir a janela de gestão de instância (ligado pela View).</summary>
    public Action<InstanceModsPageViewModel>? OpenModsWindowRequested { get; set; }

    /// <summary>Pedido para abrir a janela de seleção de mods (ligado pela View).</summary>
    public Action<ModSelectionViewModel>? OpenModSelectionRequested { get; set; }

    /// <summary>Abre a janela (própria) de seleção de mods.</summary>
    public void ShowModSelection(ModSelectionViewModel selection)
    {
        OpenModSelectionRequested?.Invoke(selection);
    }

    /// <summary>Pedido de confirmação (ligado pela View; abre um diálogo modal).</summary>
    public Func<string, string, Task<bool>>? ConfirmRequested { get; set; }

    /// <summary>Mostra um diálogo de confirmação. Sem handler, assume "sim".</summary>
    public Task<bool> ConfirmAsync(string title, string message) =>
        ConfirmRequested?.Invoke(title, message) ?? Task.FromResult(true);

    public Func<string, Task<string?>>? SaveFileRequested { get; set; }
    public Func<Task<string?>>? OpenFileRequested { get; set; }

    public Task<string?> SaveFileAsync(string suggestedName) =>
        SaveFileRequested?.Invoke(suggestedName) ?? Task.FromResult<string?>(null);

    public Task<string?> OpenFileAsync() =>
        OpenFileRequested?.Invoke() ?? Task.FromResult<string?>(null);

    // ── Estado partilhado ────────────────────────────────────────

    /// <summary>Persiste o perfil de jogo no disco. Chamado pelas páginas ao mudar definições.</summary>
    public void PersistSettings()
    {
        _settings.Save(_game);
    }

    /// <summary>Atualiza o progresso global mostrado na barra de estado.</summary>
    public void SetBusy(bool busy, double progress, string status)
    {
        IsBusy = busy;
        GlobalProgress = progress;
        StatusMessage = status;
    }
}
