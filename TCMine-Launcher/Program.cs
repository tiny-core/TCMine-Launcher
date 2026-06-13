using System;
using System.IO;
using Avalonia;
using Serilog;
using TCMine_Launcher.Services;
using Velopack;

namespace TCMine_Launcher;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack: trata hooks de instalação/atualização. TEM de ser o primeiro a correr.
        VelopackApp.Build().Run();

        LauncherPaths.EnsureRoot();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(LauncherPaths.Root, "logs", "launcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("TCMine Launcher a arrancar (v{Version})",
                typeof(Program).Assembly.GetName().Version?.ToString(3));

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Falha fatal no arranque");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}