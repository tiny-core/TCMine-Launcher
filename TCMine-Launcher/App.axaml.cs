using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;
using TCMine_Launcher.ViewModels;
using TCMine_Launcher.Views;

namespace TCMine_Launcher;

public class App : Application
{
    /// <summary>Contentor de serviços da app (composition root). Ver <see cref="ConfigureServices" />.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Construído na thread da UI: serviços que capturam o SynchronizationContext
            // (ex.: ContentWatcher) ficam ligados à thread certa.
            Services = ConfigureServices();

            // O DataContext (e o handler de janelas) é criado no construtor da MainWindow,
            // que resolve o ViewModel do contentor.
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

    /// <summary>
    ///     Composition root: regista os Models partilhados, os serviços (todos singletons)
    ///     e o shell. Os serviços que dependem do URL do servidor lêem-no do
    ///     <see cref="GameProfile" /> singleton, por isso refletem mudanças de Definições.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var s = new ServiceCollection();

        // Models partilhados
        s.AddSingleton<SettingsService>();
        s.AddSingleton(sp => sp.GetRequiredService<SettingsService>().Load()); // GameProfile
        s.AddSingleton<PlayerProfile>();

        // Serviços de infraestrutura
        s.AddSingleton<GameRunStateStore>();
        s.AddSingleton<InstanceService>();
        s.AddSingleton<AuthService>();

        // Serviços que precisam do URL do servidor (lido do GameProfile a cada pedido)
        s.AddSingleton(sp => new CurseForgeClient(() => sp.GetRequiredService<GameProfile>().ServerUrl));
        s.AddSingleton(sp => new ModInstaller(sp.GetRequiredService<CurseForgeClient>()));
        s.AddSingleton(sp => new ManifestService(() => sp.GetRequiredService<GameProfile>().ServerUrl));
        s.AddSingleton<IManifestSource>(sp => sp.GetRequiredService<ManifestService>());
        s.AddSingleton(sp => new NewsService(() => sp.GetRequiredService<GameProfile>().ServerUrl));
        s.AddSingleton(sp => new ContentWatcher(() => sp.GetRequiredService<GameProfile>().ServerUrl));
        s.AddSingleton(sp => new AppUpdater(() => sp.GetRequiredService<GameProfile>().ServerUrl));
        s.AddSingleton(sp => new ContentSyncService(
            sp.GetRequiredService<ManifestService>(),
            sp.GetRequiredService<InstanceService>().Save));

        // Shell
        s.AddSingleton<MainWindowViewModel>();

        return s.BuildServiceProvider();
    }
}
