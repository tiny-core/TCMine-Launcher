using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

public partial class ModSelectionWindow : Window
{
    public ModSelectionWindow()
    {
        InitializeComponent();
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}