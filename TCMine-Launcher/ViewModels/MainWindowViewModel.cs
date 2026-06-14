using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
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
    private readonly GameRunStateStore _runState = new();

    // Serviços
    private readonly SettingsService _settings = new();
    private readonly AppUpdater _updater;

    // ── Estado de autenticação / navegação ───────────────────────
    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty] private double _globalProgress;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RamMbDecimal))] [NotifyPropertyChangedFor(nameof(RamDisplay))]
    private double _instanceRam = 4096;

    // ── Estado da autenticação ───────────────────────────────────
    [ObservableProperty] private bool _isAuthenticating;

    // ── Progresso global (status bar) ────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEditRam))]
    private bool _isBusy;

    /// <summary>True enquanto há um Minecraft aberto — desativa ações nas páginas.</summary>
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEditRam))]
    private bool _isGameRunning;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanEditRam))]
    private bool _isLoggedIn;

    [ObservableProperty] private string _latestVersion = string.Empty;

    [ObservableProperty] private string? _loginError;
    private CancellationTokenSource? _ramSaveCts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsInstancesSelected))]
    [NotifyPropertyChangedFor(nameof(IsModpacksSelected))]
    [NotifyPropertyChangedFor(nameof(IsNewsSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private AppTab _selectedTab = AppTab.Home;

    [ObservableProperty] private string _statusMessage = "Não autenticado";

    private bool _suppressRam;

    // ── Auto-update do launcher ──────────────────────────────────
    [ObservableProperty] private bool _updateAvailable;

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

        // Se ficou um Minecraft a correr de uma sessão anterior, deteta-o.
        DetectRunningGame();

        // Tenta entrar automaticamente se houver uma sessão em cache.
        _ = TrySilentLoginAsync();

        // Verifica se há uma versão mais recente do launcher.
        _ = CheckForUpdateAsync();
    }

    /// <summary>Cliente CurseForge (via proxy), instalador de mods e manifestos.</summary>
    public CurseForgeClient CurseForge { get; }

    public ModInstaller ModInstaller { get; }
    public ManifestService Manifest { get; }
    public NewsService NewsFeed { get; }

    // ── Memória da instância ativa (editável no footer) ─────────
    // A RAM é por instância, mas o controlo vive na barra de estado (sempre acessível).
    /// <summary>RAM máxima alocável (MB) = RAM física da máquina, arredondada ao GB.</summary>
    public int RamMaximum { get; } = Math.Max(2048, SystemInfo.TotalPhysicalRamMb / 1024 * 1024);

    /// <summary>RAM atual formatada (ex.: "4096 MB") — botão da barra de estado.</summary>
    public string RamDisplay => $"{(int)InstanceRam} MB";

    /// <summary>RAM em MB para o campo numérico (NumericUpDown usa decimal).</summary>
    public decimal? RamMbDecimal
    {
        get => (decimal)InstanceRam;
        set
        {
            if (value is decimal mb) InstanceRam = (double)mb;
        }
    }

    /// <summary>A memória só é editável com sessão, instância ativa e sem instalar/jogar.</summary>
    public bool CanEditRam => IsLoggedIn && !IsGameRunning && !IsBusy && ActiveInstance is not null;

    /// <summary>Versão atual do launcher (assembly).</summary>
    public string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>Rótulo de versão mostrado na barra de estado (footer).</summary>
    public string VersionLabel => $"v{CurrentVersion}";

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

    // ── Pedidos para a camada View (janelas / diálogos / pickers) ─

    /// <summary>Pedido para abrir a janela de gestão de instância (ligado pela View).</summary>
    public Action<InstanceModsPageViewModel>? OpenModsWindowRequested { get; set; }

    /// <summary>Pedido para abrir a janela de seleção de mods (ligado pela View).</summary>
    public Action<ModSelectionViewModel>? OpenModSelectionRequested { get; set; }

    /// <summary>Pedido para abrir a janela do registo de eventos (ligado pela View).</summary>
    public Action<HomePageViewModel>? OpenLogWindowRequested { get; set; }

    /// <summary>Pedido para abrir a janela de configuração de memória (ligado pela View).</summary>
    public Action? OpenMemoryWindowRequested { get; set; }

    /// <summary>Pedido de confirmação (ligado pela View; abre um diálogo modal).</summary>
    public Func<string, string, Task<bool>>? ConfirmRequested { get; set; }

    public Func<string, Task<string?>>? SaveFileRequested { get; set; }
    public Func<Task<string?>>? OpenFileRequested { get; set; }

    partial void OnIsGameRunningChanged(bool value)
    {
        Home.NotifyGameRunningChanged();
        InstancesPage.NotifyGameRunningChanged();
        Modpacks.NotifyGameRunningChanged();
    }

    /// <summary>Limita um valor de RAM ao intervalo permitido (1 GB … RAM física).</summary>
    public int ClampRam(int mb)
    {
        return Math.Clamp(mb, 1024, RamMaximum);
    }

    partial void OnActiveInstanceChanged(MinecraftInstance? value)
    {
        _suppressRam = true;
        InstanceRam = value is null
            ? _game.AllocatedRamMb
            : ClampRam(value.RamOverrideMb ?? _game.AllocatedRamMb);
        _suppressRam = false;
        OnPropertyChanged(nameof(CanEditRam));
    }

    partial void OnInstanceRamChanged(double value)
    {
        if (_suppressRam || ActiveInstance is null) return;
        ActiveInstance.RamOverrideMb = (int)value;
        DebounceSaveRam(ActiveInstance); // evita gravar a cada "tick" do slider
    }

    private void DebounceSaveRam(MinecraftInstance instance)
    {
        _ramSaveCts?.Cancel();
        _ramSaveCts = new CancellationTokenSource();
        var ct = _ramSaveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(600, ct);
            }
            catch
            {
                return;
            }

            if (!ct.IsCancellationRequested) SaveInstance(instance);
        }, ct);
    }

    /// <summary>Regista/limpa o jogo em execução (instância + PID) para deteção ao reabrir.</summary>
    public void MarkGameStarted(string instanceId, int pid)
    {
        _runState.Save(instanceId, pid);
    }

    public void MarkGameStopped()
    {
        _runState.Clear();
    }

    /// <summary>
    ///     No arranque, se o ficheiro de estado aponta para um processo de jogo ainda
    ///     vivo, marca <see cref="IsGameRunning" /> e monitoriza a sua saída.
    /// </summary>
    private void DetectRunningGame()
    {
        var state = _runState.Load();
        if (state is null) return;

        try
        {
            var proc = Process.GetProcessById(state.Pid);
            if (proc.HasExited)
            {
                _runState.Clear();
                return;
            }

            IsGameRunning = true;
            StatusMessage = "Minecraft em execução";
            _ = MonitorReattachedAsync(proc);
        }
        catch (ArgumentException)
        {
            _runState.Clear(); // já não existe processo com esse PID
        }
    }

    private async Task MonitorReattachedAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync();
        }
        catch
        {
            /* noop */
        }

        IsGameRunning = false;
        StatusMessage = "Pronto";
        _runState.Clear();
        try
        {
            process.Dispose();
        }
        catch
        {
            /* noop */
        }
    }

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

        if (value == AppTab.Home) Home.RefreshUpdateState();
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

    /// <summary>Abre (ou traz para a frente) a janela do registo de eventos do launch.</summary>
    public void ShowLog()
    {
        OpenLogWindowRequested?.Invoke(Home);
    }

    /// <summary>Comando do botão de registo na barra de estado (footer).</summary>
    [RelayCommand]
    private void OpenLog()
    {
        ShowLog();
    }

    /// <summary>Comando do botão de memória na barra de estado (footer).</summary>
    [RelayCommand]
    private void OpenMemory()
    {
        OpenMemoryWindowRequested?.Invoke();
    }

    /// <summary>Abre a janela (própria) de seleção de mods.</summary>
    public void ShowModSelection(ModSelectionViewModel selection)
    {
        OpenModSelectionRequested?.Invoke(selection);
    }

    /// <summary>Mostra um diálogo de confirmação. Sem handler, assume "sim".</summary>
    public Task<bool> ConfirmAsync(string title, string message)
    {
        return ConfirmRequested?.Invoke(title, message) ?? Task.FromResult(true);
    }

    public Task<string?> SaveFileAsync(string suggestedName)
    {
        return SaveFileRequested?.Invoke(suggestedName) ?? Task.FromResult<string?>(null);
    }

    public Task<string?> OpenFileAsync()
    {
        return OpenFileRequested?.Invoke() ?? Task.FromResult<string?>(null);
    }

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