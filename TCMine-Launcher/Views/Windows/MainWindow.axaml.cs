using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using TCMine_Launcher.Services;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

// Views/MainWindow.axaml.cs
//
// REGRA DO CODE-BEHIND:
//   Só pode conter lógica que é estritamente da janela como janela OS
//   (drag, minimize, close, resize).
//   
//   NÃO DEVE conter:
//     ✗ Chamadas a CmlLib
//     ✗ Lógica de versões
//     ✗ Estado do perfil do jogador
//
//   TUDO o resto vai para o ViewModel.

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ZipType = new("Instância TCMine (zip)")
    {
        Patterns = new[] { "*.zip" }
    };

    private LogWindow? _logWindow;

    private MemoryWindow? _memoryWindow;

    public MainWindow()
    {
        InitializeComponent();

        // DataContext é o único "ponto de ligação" entre View e ViewModel.
        // A View nunca acede a métodos ou propriedades do ViewModel directamente
        // (só via bindings no AXAML). O VM é resolvido do contentor (DI).
        var vm = App.Services.GetRequiredService<MainWindowViewModel>();

        // Abrir uma janela secundária é responsabilidade da camada View; o ViewModel
        // apenas pede (sem referenciar tipos de janela).
        vm.OpenModsWindowRequested = OpenInstanceModsWindow;
        vm.OpenModSelectionRequested = OpenModSelectionWindow;
        vm.OpenServerListRequested = OpenServerListWindow;
        vm.OpenLogWindowRequested = OpenLogWindow;
        vm.OpenMemoryWindowRequested = OpenMemoryWindow;
        vm.ConfirmRequested = ShowConfirmAsync;
        vm.SaveFileRequested = SaveZipAsync;
        vm.OpenFileRequested = OpenZipAsync;

        DataContext = vm;

        RestoreWindowState();
        Closing += (_, _) => SaveWindowState();
    }

    private void RestoreWindowState()
    {
        var saved = WindowStateStore.Load();
        if (saved is null) return;

        if (saved.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
        else if (saved.HasPosition)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = saved.Width;
            Height = saved.Height;
            Position = new PixelPoint(saved.X, saved.Y);
        }
    }

    private void SaveWindowState()
    {
        var state = WindowStateStore.Load() ?? new LauncherWindowState();
        state.Maximized = WindowState == WindowState.Maximized;
        if (!state.Maximized)
        {
            state.Width = Width;
            state.Height = Height;
            state.X = Position.X;
            state.Y = Position.Y;
            state.HasPosition = true;
        }

        WindowStateStore.Save(state);
    }

    private async Task<string?> SaveZipAsync(string suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = "zip",
            FileTypeChoices = new[] { ZipType }
        });
        return file?.Path.LocalPath;
    }

    private async Task<string?> OpenZipAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { ZipType }
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    private void OpenInstanceModsWindow(InstanceModsPageViewModel modsViewModel)
    {
        var window = new InstanceModsWindow { DataContext = modsViewModel };
        // Ao fechar, atualiza a lista de instâncias (nome/versões podem ter mudado).
        window.Closed += (_, _) => (DataContext as MainWindowViewModel)?.RefreshInstancesDisplay();
        _ = window.ShowDialog(this);
    }

    private void OpenModSelectionWindow(ModSelectionViewModel selection)
    {
        // Não-modal (Topmost) para flutuar mesmo sobre a janela de gestão.
        new ModSelectionWindow { DataContext = selection }.Show();
    }

    private void OpenServerListWindow(InstanceModsPageViewModel page)
    {
        // Não-modal (Topmost) para flutuar sobre a janela de gestão da instância.
        new ServerListWindow { DataContext = page }.Show();
    }

    private void OpenLogWindow(HomePageViewModel logViewModel)
    {
        // Instância única: se já estiver aberta, traz para a frente.
        if (_logWindow is not null)
        {
            _logWindow.Activate();
            return;
        }

        _logWindow = new LogWindow { DataContext = logViewModel };
        _logWindow.Closed += (_, _) => _logWindow = null;
        _logWindow.Show(this);
    }

    private void OpenMemoryWindow()
    {
        // Instância única: se já estiver aberta, traz para a frente.
        if (_memoryWindow is not null)
        {
            _memoryWindow.Activate();
            return;
        }

        // Partilha o DataContext (shell) — a janela edita a RAM da instância ativa.
        _memoryWindow = new MemoryWindow { DataContext = DataContext };
        _memoryWindow.Closed += (_, _) => _memoryWindow = null;
        _memoryWindow.Show(this);
    }

    private async Task<bool> ShowConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        return await dialog.ShowDialog<bool>(this);
    }
}