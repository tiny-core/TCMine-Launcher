using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
    }

    public ConfirmDialog(string title, string message) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}