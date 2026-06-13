using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TCMine_Launcher.ViewModels;
using TCMine_Launcher.Views;

namespace TCMine_Launcher;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            // O DataContext (e o handler de janelas) é criado no construtor da MainWindow.
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}