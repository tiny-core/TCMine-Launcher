using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
        {
            // O DataContext (e o handler de janelas) é criado no construtor da MainWindow.
            var main = new MainWindow();
            desktop.MainWindow = main;
            main.Show();

            // Splash por cima, fechado após um momento.
            var splash = new SplashWindow();
            splash.Show();
            DispatcherTimer.RunOnce(
                () => splash.Close(),
                TimeSpan.FromMilliseconds(1200));
        }

        base.OnFrameworkInitializationCompleted();
    }
}