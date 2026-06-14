using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core.Auth;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Orquestra o pipeline de preparação do jogo (lógica de domínio, sem UI):
///     instala/garante NeoForge e o processo (<see cref="GameLauncher" />), descarrega
///     os mods em falta (<see cref="ModInstaller" />), aplica o bundle de overrides do
///     modpack e repõe as configs do jogador vindas do servidor. NÃO arranca o processo
///     — devolve-o pronto, para o chamador ligar a captura de logs antes de
///     <see cref="Process.Start" />. Mantém o ViewModel fino (só estado de UI + arranque
///     e monitorização do processo).
/// </summary>
public class LaunchOrchestrator
{
    private readonly PlayerConfigService _configSync = new();
    private readonly GameLauncher _launcher = new();
    private readonly ModInstaller _mods;
    private readonly OverridesInstaller _overrides = new();

    public LaunchOrchestrator(ModInstaller mods)
    {
        _mods = mods;
    }

    /// <summary>
    ///     Prepara (sem arrancar) o processo do jogo para uma instância. Reporta progresso
    ///     via <paramref name="progress" /> e respeita o <paramref name="ct" />.
    /// </summary>
    public async Task<Process> PrepareAsync(
        MinecraftInstance instance, MSession session, int ramMb, string? javaPath, string? serverUrl,
        IProgress<LaunchProgress> progress, CancellationToken ct)
    {
        var gameDir = LauncherPaths.InstanceGameDir(instance.Id);
        var autoJoin = instance.Servers.FirstOrDefault(s => s.Name == instance.AutoJoinServerName);

        var process = await _launcher.PrepareAsync(
            gameDir, instance.MinecraftVersion, instance.NeoForgeVersion, session, ramMb, javaPath,
            progress, ct, instance.Servers, autoJoin);

        // Com overrides não fazemos prune (eles podem trazer jars próprios).
        await _mods.EnsureModsAsync(instance, progress, ct, instance.IsOfficial && !instance.HasOverrides);

        // Bundle de overrides (configs/resourcepacks/options), uma vez por versão.
        progress.Report(new LaunchProgress(
            LaunchState.DownloadingAssets, 100, "A aplicar configuração do modpack..."));
        await _overrides.EnsureAsync(instance, serverUrl, ct);

        // Repõe as configs do jogador do servidor se forem mais recentes (sync entre PCs).
        await _configSync.PullAsync(instance, session.UUID, session.AccessToken, serverUrl, ct);

        return process;
    }

    /// <summary>Guarda no servidor as configs alteradas na sessão (chamado ao fechar o jogo).</summary>
    public Task PushConfigsAsync(MinecraftInstance instance, string? uuid, string? accessToken,
        string? serverUrl) =>
        _configSync.PushAsync(instance, uuid, accessToken, serverUrl);
}
