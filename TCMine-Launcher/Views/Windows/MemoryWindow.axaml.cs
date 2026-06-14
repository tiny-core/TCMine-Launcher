using Avalonia.Controls;
using Avalonia.Interactivity;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

public partial class MemoryWindow : Window
{
    public MemoryWindow()
    {
        InitializeComponent();
    }

    // Guarda a RAM e fecha a janela (fluxo esperado pelo utilizador).
    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as MainWindowViewModel)?.SaveRamCommand.Execute(null);
        Close();
    }
}
