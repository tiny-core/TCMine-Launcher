using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

public partial class ServerListWindow : Window
{
    public ServerListWindow()
    {
        InitializeComponent();
    }

    private void Done_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
