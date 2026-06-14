using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

public partial class UpdateWindow : Window
{
    public UpdateWindow()
    {
        InitializeComponent();
    }

    private void Later_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
