using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

public partial class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/tiny-core/TCMine-Launcher";

    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"v{version}";
    }

    private void Github_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        }
        catch
        {
            // ignora se não houver navegador disponível
        }
    }
}