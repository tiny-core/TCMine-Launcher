using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    // Serviços
    private readonly AuthService _auth = new();
    private readonly SettingsService _settings = new();
    private readonly InstanceService _instances = new();

    /// <summary>Cliente CurseForge (via proxy), instalador de mods e manifestos.</summary>
    public CurseForgeClient CurseForge { get; }
    public ModInstaller ModInstaller { get; }
    public ManifestService Manifest { get; }
    public NewsService NewsFeed { get; }
    private readonly AppUpdater _updater;

    // ── Auto-update do launcher ──────────────────────────────────
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _latestVersion = string.Empty;

    /// <summary>Todas as instâncias instaladas (fonte única, partilhada com a página).</summary>
    public ObservableCollection<MinecraftInstance> Instances { get; } = new();

    /// <summary>Instância atualmente selecionada (a que a Home lança).</summary>
    [ObservableProperty] private MinecraftInstance? _activeInstance;

    /// <summary>Sessão Minecraft activa (usada depois para lançar o jogo).</summary>
    private MSession? _session;

    /// <summary>Sessão activa, exposta às páginas que precisam de lançar o jogo.</summary>
    public MSession? CurrentSession => _session;

    /// <summary>Permite cancelar o login interactivo em curso.</summary>
    private CancellationTokenSource? _loginCts;

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

    // ── Comandos de navegação ────────────────────────────────────
    [RelayCommand]
    private void Navigate(AppTab tab)
    {
        SelectedTab = tab;
    }

    // ── Autenticação Microsoft (navegador do sistema) ────────────
    [RelayCommand]
    private async Task LoginMicrosoftAsync()
    {
        if (IsAuthenticating) return;

        IsAuthenticating = true;
        LoginError = null;
        StatusMessage = "A autenticar com a Microsoft...";

        // Timeout de segurança: se o utilizador fechar o browser, não fica preso.
        _loginCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            var session = await _auth.LoginAsync(_loginCts.Token);
            ApplySession(session, AccountType.Microsoft);
            EnterApp("Pronto");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Login cancelado";
        }
        catch (Exception ex)
        {
            var detail = Flatten(ex);
            LoginError = detail.Contains("403") || detail.Contains("Forbidden")
                ? "Autenticaste com sucesso, mas a aplicação do Azure ainda não tem permissão " +
                  "para a API do Minecraft (erro 403). Pede aprovação em https://aka.ms/mce-reviewappid."
                : "Falha no login: " + detail;
            StatusMessage = "Não autenticado";
        }
        finally
        {
            _loginCts?.Dispose();
            _loginCts = null;
            IsAuthenticating = false;
        }
    }

    [RelayCommand]
    private void CancelLogin()
    {
        _loginCts?.Cancel();
    }

    /// <summary>Junta as mensagens de toda a cadeia de InnerException.</summary>
    private static string Flatten(Exception ex)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add(e.Message);
        return string.Join(" → ", parts);
    }

    /// <summary>Login silencioso no arranque (não mostra erro se não houver conta).</summary>
    private async Task TrySilentLoginAsync()
    {
        try
        {
            var session = await _auth.LoginSilentAsync();
            ApplySession(session, AccountType.Microsoft);
            EnterApp("Pronto");
        }
        catch
        {
            // Sem sessão em cache — fica na tela de login.
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _auth.SignOutAsync();
        }
        catch
        {
            // Best-effort: mesmo que falhe, limpamos o estado local.
        }

        _session = null;
        _player.Name = "Steve";
        _player.Uuid = string.Empty;
        _player.AccountType = AccountType.Offline;
        RefreshPlayer();

        IsLoggedIn = false;
        StatusMessage = "Não autenticado";
    }

    /// <summary>Mapeia a sessão devolvida pelo CmlLib para o perfil (dados puros).</summary>
    private void ApplySession(MSession session, AccountType type)
    {
        _session = session;
        _player.Name = session.Username ?? "Player";
        _player.Uuid = session.UUID ?? string.Empty;
        _player.AccountType = type;
    }

    private void EnterApp(string status)
    {
        RefreshPlayer();
        SelectedTab = AppTab.Home;
        IsLoggedIn = true;
        StatusMessage = status;
    }

    // ── Gestão de instâncias ─────────────────────────────────────

    /// <summary>Carrega as instâncias do disco e restaura a que estava selecionada.</summary>
    private void LoadInstances()
    {
        Instances.Clear();
        foreach (var instance in _instances.LoadAll())
            Instances.Add(instance);

        // Primeira execução: cria uma instância inicial (deletável). Os modpacks
        // oficiais vêm do servidor, na aba Modpacks.
        if (Instances.Count == 0)
        {
            var seed = _instances.Create(
                "Instância padrão", _game.MinecraftVersion, _game.NeoForgeVersion);
            Instances.Add(seed);
        }

        ActiveInstance =
            Instances.FirstOrDefault(i => i.Id == _game.SelectedInstanceId)
            ?? Instances.First();
    }

    /// <summary>Define a instância ativa (a que a Home lança) e persiste a escolha.</summary>
    public void SelectInstance(MinecraftInstance instance)
    {
        ActiveInstance = instance;
        _game.SelectedInstanceId = instance.Id;
        PersistSettings();
        Home.NotifyInstanceChanged();
        InstancesPage.NotifyActiveChanged();
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

    /// <summary>Pedido para abrir a janela de gestão de instância (ligado pela View).</summary>
    public Action<InstanceModsPageViewModel>? OpenModsWindowRequested { get; set; }

    /// <summary>Pedido de confirmação (ligado pela View; abre um diálogo modal).</summary>
    public Func<string, string, Task<bool>>? ConfirmRequested { get; set; }

    /// <summary>Mostra um diálogo de confirmação. Sem handler, assume "sim".</summary>
    public Task<bool> ConfirmAsync(string title, string message) =>
        ConfirmRequested?.Invoke(title, message) ?? Task.FromResult(true);

    // ── Pickers de ficheiro (ligados pela View) ──────────────────
    public Func<string, Task<string?>>? SaveFileRequested { get; set; }
    public Func<Task<string?>>? OpenFileRequested { get; set; }

    public Task<string?> SaveFileAsync(string suggestedName) =>
        SaveFileRequested?.Invoke(suggestedName) ?? Task.FromResult<string?>(null);

    public Task<string?> OpenFileAsync() =>
        OpenFileRequested?.Invoke() ?? Task.FromResult<string?>(null);

    /// <summary>Abre a pasta do jogo da instância no explorador de ficheiros.</summary>
    public void OpenInstanceFolder(MinecraftInstance instance)
    {
        var dir = LauncherPaths.InstanceGameDir(instance.Id);
        Directory.CreateDirectory(dir);
        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch
        {
            // ignora falhas a abrir o explorador
        }
    }

    /// <summary>Exporta uma instância para um zip.</summary>
    public void ExportInstance(MinecraftInstance instance, string zipPath)
    {
        _instances.Export(instance, zipPath);
    }

    /// <summary>Importa uma instância de um zip, adiciona-a e seleciona-a.</summary>
    public MinecraftInstance ImportInstance(string zipPath)
    {
        var instance = _instances.Import(zipPath);
        Instances.Insert(0, instance);
        SelectInstance(instance);
        return instance;
    }

    /// <summary>Abre a gestão de mods/instância numa janela separada.</summary>
    public void ShowInstanceMods(MinecraftInstance instance, bool isNew = false)
    {
        InstanceModsPage.Begin(instance, isNew);
        OpenModsWindowRequested?.Invoke(InstanceModsPage);
    }

    /// <summary>Volta das páginas de criação/mods para a lista de instâncias.</summary>
    public void BackToInstances()
    {
        SelectedTab = AppTab.Instances;
        CurrentPage = InstancesPage;
    }

    /// <summary>Indica se uma instância já tem ficheiros de jogo (define Jogar vs Instalar).</summary>
    public bool IsInstanceInstalled(MinecraftInstance instance)
    {
        return _instances.IsInstalled(instance);
    }

    /// <summary>Cria uma nova instância, persiste-a e seleciona-a.</summary>
    public MinecraftInstance CreateInstance(string name, string mcVersion, string neoForgeVersion)
    {
        var instance = _instances.Create(name, mcVersion, neoForgeVersion);
        Instances.Insert(0, instance);
        SelectInstance(instance);
        return instance;
    }

    /// <summary>Grava as alterações de uma instância no disco.</summary>
    public void SaveInstance(MinecraftInstance instance)
    {
        _instances.Save(instance);
    }

    /// <summary>
    ///     Recarrega as instâncias do disco (após edição numa janela) para os cartões
    ///     refletirem nome/versões atualizados. Preserva a instância ativa pelo Id.
    /// </summary>
    public void RefreshInstancesDisplay()
    {
        var activeId = ActiveInstance?.Id;
        Instances.Clear();
        foreach (var instance in _instances.LoadAll())
            Instances.Add(instance);

        ActiveInstance = Instances.FirstOrDefault(i => i.Id == activeId)
                         ?? Instances.FirstOrDefault();

        Home.NotifyInstanceChanged();
        InstancesPage.NotifyActiveChanged();
    }

    /// <summary>
    ///     Cria uma cópia editável (Manual) de uma instância como <b>rascunho</b> em
    ///     memória — NÃO grava em disco nem aparece na lista até ser concluída
    ///     (<see cref="CommitInstance" />). Útil para personalizar a partir de um
    ///     modpack oficial sem o alterar.
    /// </summary>
    public MinecraftInstance DuplicateInstance(MinecraftInstance source)
    {
        return new MinecraftInstance
        {
            Name = source.Name + " (cópia)",
            MinecraftVersion = source.MinecraftVersion,
            NeoForgeVersion = source.NeoForgeVersion,
            Source = InstanceSource.Manual,
            RamOverrideMb = source.RamOverrideMb,
            Mods = source.Mods
                .Select(m => new ModEntry
                {
                    ModId = m.ModId, FileId = m.FileId, Name = m.Name,
                    FileName = m.FileName, DownloadUrl = m.DownloadUrl
                }).ToList(),
            Servers = source.Servers
                .Select(s => new ServerEntry { Name = s.Name, Address = s.Address, Port = s.Port })
                .ToList()
        };
    }

    /// <summary>
    ///     Persiste uma instância editada na janela. Para um rascunho novo
    ///     (<paramref name="isNew" />), grava-o, adiciona-o à lista e seleciona-o.
    /// </summary>
    public void CommitInstance(MinecraftInstance instance, bool isNew)
    {
        _instances.Save(instance);

        if (isNew)
        {
            if (!Instances.Contains(instance))
                Instances.Insert(0, instance);
            SelectInstance(instance);
        }
    }

    /// <summary>Elimina uma instância (e a sua pasta). Garante que sobra sempre uma ativa.</summary>
    public void DeleteInstance(MinecraftInstance instance)
    {
        _instances.Delete(instance);
        Instances.Remove(instance);

        if (ActiveInstance == instance)
            SelectInstance(Instances.FirstOrDefault() ?? CreateSeedAfterDelete());
    }

    /// <summary>Se o utilizador apagar a última instância, recria uma inicial.</summary>
    private MinecraftInstance CreateSeedAfterDelete()
    {
        var seed = _instances.Create(
            "Instância padrão", _game.MinecraftVersion, _game.NeoForgeVersion);
        Instances.Add(seed);
        return seed;
    }

    /// <summary>
    ///     Instala (ou atualiza) uma instância a partir de um manifesto oficial:
    ///     copia versões, mods e servidores. Se já existir uma instância desse
    ///     modpack, atualiza-a em vez de duplicar. Seleciona-a no fim.
    /// </summary>
    public MinecraftInstance InstallFromManifest(ModpackManifest manifest)
    {
        var existing = Instances.FirstOrDefault(i => i.ModpackId == manifest.Id);
        if (existing is not null)
        {
            existing.Name = manifest.Name;
            existing.MinecraftVersion = manifest.Minecraft;
            existing.NeoForgeVersion = manifest.Neoforge;
            existing.ManifestVersion = manifest.Version;
            existing.Mods = manifest.Mods;
            existing.Servers = manifest.Servers;
            _instances.Save(existing);
            SelectInstance(existing);
            return existing;
        }

        var instance = new MinecraftInstance
        {
            Name = manifest.Name,
            MinecraftVersion = manifest.Minecraft,
            NeoForgeVersion = manifest.Neoforge,
            Source = InstanceSource.OfficialManifest,
            ModpackId = manifest.Id,
            ManifestVersion = manifest.Version,
            Mods = manifest.Mods,
            Servers = manifest.Servers
        };
        _instances.Save(instance);
        Instances.Insert(0, instance);
        SelectInstance(instance);
        return instance;
    }

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
}
