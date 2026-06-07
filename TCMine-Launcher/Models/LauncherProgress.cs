namespace TCMine_Launcher.Models;

/// <summary>
///     Representa as fases possíveis do processo de lançamento.
///     Definido no Model porque é lógica de negócio — não lógica de UI.
/// </summary>
public enum LaunchState
{
    Idle,
    CheckingFiles,
    DownloadingAssets,
    InstallingNeoForge,
    PreparingJvm,
    Launching,
    Running,
    Failed
}

/// <summary>
///     Snapshot imutável do progresso do launch num dado momento.
///     Record: igualdade por valor, fácil de passar entre camadas.
/// </summary>
public record LaunchProgress(LaunchState State, double Percent, string Message)
{
    /// <summary>Estado inicial — nada em curso.</summary>
    public static LaunchProgress Idle => new(LaunchState.Idle, 0, "Pronto");

    /// <summary>O launch está activamente a decorrer (não idle nem falhou).</summary>
    public bool IsActive => State is not (LaunchState.Idle or LaunchState.Failed);
}