using System;
using System.IO;
using System.Text.Json;

namespace TCMine_Launcher.Services;

/// <summary>
///     Persiste qual instância tem o jogo a correr (id + PID do processo), para o
///     launcher detetar um jogo já aberto quando é reaberto enquanto o Minecraft
///     continua em execução.
/// </summary>
public class GameRunStateStore
{
    public record RunState(string InstanceId, int Pid, DateTimeOffset StartedAt);

    public void Save(string instanceId, int pid)
    {
        try
        {
            LauncherPaths.EnsureRoot();
            File.WriteAllText(LauncherPaths.RunStateFile,
                JsonSerializer.Serialize(new RunState(instanceId, pid, DateTimeOffset.Now)));
        }
        catch
        {
            // best-effort — a deteção é um extra, não pode partir o launch
        }
    }

    public void Clear()
    {
        try { File.Delete(LauncherPaths.RunStateFile); }
        catch { /* noop */ }
    }

    public RunState? Load()
    {
        try
        {
            return File.Exists(LauncherPaths.RunStateFile)
                ? JsonSerializer.Deserialize<RunState>(File.ReadAllText(LauncherPaths.RunStateFile))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
