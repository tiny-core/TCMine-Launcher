using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

        DataContext = vm;
    }

    private void OpenInstanceModsWindow(InstanceModsPageViewModel modsViewModel)
    {
        var window = new InstanceModsWindow { DataContext = modsViewModel };
        // Ao fechar, atualiza a lista de instâncias (nome/versões podem ter mudado).
        window.Closed += (_, _) => (DataContext as MainWindowViewModel)?.RefreshInstancesDisplay();
        _ = window.ShowDialog(this);
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