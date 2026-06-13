using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    public MainWindow()
    {
        InitializeComponent();

        // DataContext é o único "ponto de ligação" entre View e ViewModel.
        // A View nunca acede a métodos ou propriedades do ViewModel directamente
        // (só via bindings no AXAML).
        var vm = new MainWindowViewModel();

        // Abrir uma janela secundária é responsabilidade da camada View; o ViewModel
        // apenas pede (sem referenciar tipos de janela).
        vm.OpenModsWindowRequested = OpenInstanceModsWindow;
        vm.ConfirmRequested = ShowConfirmAsync;
        vm.SaveFileRequested = SaveZipAsync;
        vm.OpenFileRequested = OpenZipAsync;

        DataContext = vm;
    }

    private static readonly FilePickerFileType ZipType = new("Instância TCMine (zip)")
    {
        Patterns = new[] { "*.zip" }
    };

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

    private async System.Threading.Tasks.Task<bool> ShowConfirmAsync(string title, string message)
    {
        var dialog = new ConfirmDialog(title, message);
        return await dialog.ShowDialog<bool>(this);
    }

    // ── Lógica de janela OS (único código legítimo aqui) ─────────

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}