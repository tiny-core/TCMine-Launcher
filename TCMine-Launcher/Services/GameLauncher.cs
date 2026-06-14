using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.Installer.NeoForge;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installers;
using CmlLib.Core.ProcessBuilder;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Instalação e lançamento reais do Minecraft + NeoForge via CmlLib.Core.
///     Substitui a simulação que existia em <c>HomePageViewModel.PlayAsync</c>.
///     Reporta o progresso através de <see cref="LaunchProgress" /> (Model puro),
///     traduzindo os eventos de baixo nível do CmlLib em fases legíveis.
/// </summary>
public class GameLauncher
{
    /// <summary>
    ///     Instala (se preciso) o NeoForge para a versão pedida, garante os ficheiros
    ///     do jogo e constrói o processo pronto a arrancar. Não chama <c>Start()</c> —
    ///     isso fica a cargo de quem invoca, para poder ligar logs antes de arrancar.
    /// </summary>
    public async Task<Process> PrepareAsync(
        string gameDir,
        string mcVersion,
        string neoForgeVersion,
        MSession session,
        int ramMb,
        string? javaPath,
        IProgress<LaunchProgress> progress,
        CancellationToken ct = default,
        IReadOnlyList<ServerEntry>? servers = null,
        ServerEntry? autoJoinServer = null)
    {
        Directory.CreateDirectory(gameDir);

        // Garante que os servidores do modpack aparecem na lista multijogador.
        if (servers is { Count: > 0 })
            ServersDatWriter.Ensure(gameDir, servers);

        var launcher = new MinecraftLauncher(gameDir);

        // Normaliza o caminho do Java: vazio => deixa o CmlLib auto-detetar/instalar.
        var resolvedJava = string.IsNullOrWhiteSpace(javaPath) ? null : javaPath;

        // Traduz o progresso por-ficheiro do CmlLib numa percentagem para a UI.
        void OnFileProgress(object? _, InstallerProgressChangedEventArgs e)
        {
            var pct = e.TotalTasks > 0 ? (double)e.ProgressedTasks / e.TotalTasks * 100 : 0;
            progress.Report(new LaunchProgress(
                LaunchState.DownloadingAssets, pct, e.Name ?? "A processar ficheiros..."));
        }

        launcher.FileProgressChanged += OnFileProgress;
        try
        {
            // 1. NeoForge (resolve a versão, baixa o vanilla base e corre o instalador).
            progress.Report(new LaunchProgress(
                LaunchState.InstallingNeoForge, 0, $"A instalar NeoForge {neoForgeVersion}..."));

            var installer = new NeoForgeInstaller(launcher);
            var versionName = await installer.Install(mcVersion, neoForgeVersion, new NeoForgeInstallOptions
            {
                JavaPath = resolvedJava,
                SkipIfAlreadyInstalled = true,
                CancellationToken = ct
            });

            // 2. Garante o resto dos ficheiros (assets/libs) e constrói o processo.
            progress.Report(new LaunchProgress(
                LaunchState.PreparingJvm, 95, "A preparar a JVM..."));

            var launchOption = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = ramMb,
                JavaPath = resolvedJava
            };

            // Ligação direta ao servidor (entra logo no jogo).
            if (autoJoinServer is not null)
            {
                launchOption.ServerIp = autoJoinServer.Address;
                launchOption.ServerPort = autoJoinServer.Port;
            }

            var process = await launcher.InstallAndBuildProcessAsync(versionName, launchOption);

            // Captura o stdout/stderr do jogo (log + deteção de crash).
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            progress.Report(new LaunchProgress(
                LaunchState.Launching, 100, "A iniciar o Minecraft..."));
            return process;
        }
        finally
        {
            launcher.FileProgressChanged -= OnFileProgress;
        }
    }
}