using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     ViewModel da janela principal.
///     RESPONSABILIDADES:
///     ✓ Mantém instâncias dos Models (_playerProfile, _gameProfile)
///     ✓ Expõe propriedades observáveis que a View pode bindear
///     ✓ Sincroniza alterações da UI de volta para os Models
///     ✓ Contém os Commands que respondem às ações do utilizador
///     ✓ Contém estado de UI (IsLaunching, LaunchProgress) — NÃO vai para o Model
///     NÃO FAZ:
///     ✗ Não conhece nenhum controlo Avalonia (Button, TextBlock, etc.)
///     ✗ Não toca em AXAML
///     ✗ Não chama métodos de UI diretamente
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly GameProfile _gameProfile;
    // ═════════════════════════════════════════════════════════════
    //  MODELS (dados puros, sem UI)
    //  O ViewModel cria e gere estes objetos.
    //  A View nunca os vê diretamente.
    // ═════════════════════════════════════════════════════════════

    private readonly PlayerProfile _playerProfile;

    // ═════════════════════════════════════════════════════════════
    //  ESTADO DE UI (só existe no ViewModel — os Models não sabem)
    //
    //  Exemplos: "está a lançar?", "qual o progresso?", "mensagem de estado"
    //  Nada disto faz sentido no Model (GameProfile não precisa saber
    //  que a barra de progresso está a 60%).
    // ═════════════════════════════════════════════════════════════

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(LaunchCommand))] // re-avalia CanLaunch() quando muda
    private bool _isLaunching;

    [ObservableProperty] private string _javaVersion = "Java 21";

    [ObservableProperty] private double _launchProgress;

    [ObservableProperty] private string _launchStatus = string.Empty;

    // Listas disponíveis (futuro: carregadas de API/disco via CmlLib)
    [ObservableProperty] private ObservableCollection<string> _minecraftVersions;

    [ObservableProperty] private ObservableCollection<string> _neoForgeVersions;

    // ═════════════════════════════════════════════════════════════
    //  PROPRIEDADES OBSERVÁVEIS (wrappers dos dados do Model)
    //
    //  [ObservableProperty] gera automaticamente:
    //    • A propriedade pública (ex: PlayerName)
    //    • SetProperty() com INotifyPropertyChanged
    //    • Método partial OnXxxChanged() para lógica extra
    //
    //  A View faz {Binding PlayerName} e é notificada quando muda.
    // ═════════════════════════════════════════════════════════════

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(AvatarInitials))] // quando Name muda, Initials também
    private string _playerName;

    [ObservableProperty] private string? _selectedMinecraftVersion;

    [ObservableProperty] private string? _selectedNeoForgeVersion;

    [ObservableProperty] private string _statusMessage = "Pronto";

    // ═════════════════════════════════════════════════════════════
    //  CONSTRUTOR
    // ═════════════════════════════════════════════════════════════

    public MainWindowViewModel()
    {
        // 1. Criar os Models
        _playerProfile = new PlayerProfile();
        _gameProfile = new GameProfile();

        // 2. Inicializar as propriedades observáveis A PARTIR dos Models
        //    (o "estado inicial" vem sempre do Model, nunca hardcoded aqui)
        _playerName = _playerProfile.Name;
        _selectedMinecraftVersion = _gameProfile.MinecraftVersion;
        _selectedNeoForgeVersion = _gameProfile.NeoForgeVersion;

        // 3. Listas de versões (futuramente: carregar via CmlLib.GetAllVersions())
        _minecraftVersions = new ObservableCollection<string>
        {
            "1.21.4", "1.21.3", "1.21.1", "1.20.6", "1.20.4", "1.20.1"
        };
        _neoForgeVersions = new ObservableCollection<string>
        {
            "21.1.172", "21.1.171", "21.1.170", "21.1.165", "21.1.160"
        };
    }

    // ═════════════════════════════════════════════════════════════
    //  PROPRIEDADES COMPUTADAS  (calculadas a partir do Model)
    //  Não precisam de [ObservableProperty] — dependem de outras
    //  propriedades que já notificam quando mudam.
    // ═════════════════════════════════════════════════════════════

    /// <summary>Iniciais para o avatar — delegado ao Model.</summary>
    public string AvatarInitials => _playerProfile.ComputeInitials();

    /// <summary>Texto do tipo de conta — delegado ao Model.</summary>
    public string AccountLabel => _playerProfile.AccountLabel;

    // ═════════════════════════════════════════════════════════════
    //  SINCRONIZAÇÃO ViewModel → Model
    //
    //  Quando o utilizador altera algo na View (ex: escreve um nome),
    //  o binding atualiza a propriedade observável aqui.
    //  Os métodos partial OnXxxChanged() sincronizam de volta para o Model.
    //
    //  Por quê? Para que o Model tenha sempre os dados actualizados
    //  e possa ser guardado em disco a qualquer momento.
    // ═════════════════════════════════════════════════════════════

    partial void OnPlayerNameChanged(string value)
    {
        _playerProfile.Name = value;
        // AvatarInitials é computado do Model, forçar notificação
        OnPropertyChanged(nameof(AvatarInitials));
    }

    partial void OnSelectedMinecraftVersionChanged(string? value)
    {
        if (value is not null)
            _gameProfile.MinecraftVersion = value;
    }

    partial void OnSelectedNeoForgeVersionChanged(string? value)
    {
        if (value is not null)
            _gameProfile.NeoForgeVersion = value;
    }

    // ═════════════════════════════════════════════════════════════
    //  COMMAND DE LAUNCH
    //
    //  [RelayCommand] gera LaunchCommand (IAsyncRelayCommand).
    //  A View faz {Binding LaunchCommand} no botão — nunca chama
    //  LaunchAsync() directamente.
    //
    //  CanExecute = nameof(CanLaunch) define quando o botão está activo.
    //  [NotifyCanExecuteChangedFor] acima garante que é reavaliado
    //  quando IsLaunching muda.
    // ═════════════════════════════════════════════════════════════

    private bool CanLaunch()
    {
        return !IsLaunching;
    }

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private async Task LaunchAsync()
    {
        if (SelectedMinecraftVersion is null || SelectedNeoForgeVersion is null)
            return;

        IsLaunching = true;
        LaunchProgress = 0;

        try
        {
            // ─────────────────────────────────────────────────────
            //  TODO: Substituir pela integração real com CmlLib
            //
            //  var path     = new MinecraftPath();
            //  var launcher = new CMLauncher(path);
            //
            //  launcher.FileChanged += (e) => {
            //      LaunchStatus   = $"[{e.FileKind}] {e.FileName}";
            //      LaunchProgress = e.ProgressedFileCount * 100.0
            //                       / e.TotalFileCount;
            //  };
            //
            //  // O Model tem os dados, o ViewModel usa-os:
            //  var launchOption = new MLaunchOption
            //  {
            //      Session       = MSession.GetOfflineSession(_playerProfile.Name),
            //      MinimumRamMb  = 512,
            //      MaximumRamMb  = _gameProfile.AllocatedRamMb,
            //  };
            //
            //  var process = await launcher.CreateProcessAsync(
            //      _gameProfile.MinecraftVersion, launchOption);
            //  process.Start();
            // ─────────────────────────────────────────────────────

            await SimulateLaunchAsync();
        }
        catch (Exception ex)
        {
            LaunchStatus = $"Erro: {ex.Message}";
            StatusMessage = "Falhou";
        }
        finally
        {
            await Task.Delay(1500);
            IsLaunching = false;
            LaunchProgress = 0;
            LaunchStatus = string.Empty;
            StatusMessage = "Pronto";
        }
    }

    // Simulação — remover quando CmlLib estiver integrado
    private async Task SimulateLaunchAsync()
    {
        var steps = new (int Pct, LaunchState State, string Msg)[]
        {
            (10, LaunchState.CheckingFiles, "A verificar ficheiros..."),
            (30, LaunchState.DownloadingAssets, "A descarregar assets..."),
            (55, LaunchState.InstallingNeoForge, "A instalar NeoForge..."),
            (80, LaunchState.PreparingJvm, "A preparar JVM..."),
            (100, LaunchState.Launching, "A iniciar Minecraft...")
        };

        foreach (var (pct, _, msg) in steps)
        {
            await Task.Delay(650);
            LaunchProgress = pct;
            LaunchStatus = msg;
            StatusMessage = msg;
        }

        await Task.Delay(600);
        StatusMessage = $"Minecraft {SelectedMinecraftVersion} a correr";
    }
}