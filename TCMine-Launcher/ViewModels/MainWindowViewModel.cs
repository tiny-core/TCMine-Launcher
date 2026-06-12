using System;
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

    public MainWindowViewModel()
    {
        _player = new PlayerProfile();
        _game = _settings.Load();

        Home = new HomePageViewModel(_player, _game, this);
        Modpacks = new ModpacksPageViewModel();
        News = new NewsPageViewModel();
        Settings = new SettingsPageViewModel(_player, _game, this);

        _currentPage = Home;

        // Tenta entrar automaticamente se houver uma sessão em cache.
        _ = TrySilentLoginAsync();
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
        Home.NotifyPlayerChanged();
        Settings.NotifyPlayerChanged();
    }
}
