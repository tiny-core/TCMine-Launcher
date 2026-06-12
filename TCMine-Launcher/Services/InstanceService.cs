using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Gere o ciclo de vida das instâncias em disco: cada uma vive em
///     <c>instances/&lt;id&gt;/</c> com o seu <c>instance.json</c> e o seu
///     <c>.minecraft</c> isolado. Escrita atómica (.tmp + move).
/// </summary>
public class InstanceService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Carrega todas as instâncias válidas, ordenadas pela mais recente jogada.</summary>
    public List<MinecraftInstance> LoadAll()
    {
        var result = new List<MinecraftInstance>();

        if (!Directory.Exists(LauncherPaths.InstancesDir))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(LauncherPaths.InstancesDir))
        {
            var configFile = Path.Combine(dir, "instance.json");
            if (!File.Exists(configFile)) continue;

            try
            {
                var instance = JsonSerializer.Deserialize<MinecraftInstance>(
                    File.ReadAllText(configFile), Options);
                if (instance is not null) result.Add(instance);
            }
            catch
            {
                // Instância corrompida — ignora em vez de derrubar o launcher.
            }
        }

        return result
            .OrderByDescending(i => i.LastPlayedAt ?? i.CreatedAt)
            .ToList();
    }

    /// <summary>Grava (ou atualiza) uma instância no disco.</summary>
    public void Save(MinecraftInstance instance)
    {
        var dir = LauncherPaths.InstanceDir(instance.Id);
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(instance, Options);
        var target = LauncherPaths.InstanceConfigFile(instance.Id);
        var tmp = target + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, target, true);
    }

    /// <summary>Cria uma nova instância já persistida.</summary>
    public MinecraftInstance Create(string name, string mcVersion, string neoForgeVersion,
        InstanceSource source = InstanceSource.Manual)
    {
        var instance = new MinecraftInstance
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Nova Instância" : name.Trim(),
            MinecraftVersion = mcVersion,
            NeoForgeVersion = neoForgeVersion,
            Source = source
        };
        Save(instance);
        return instance;
    }

    /// <summary>Remove uma instância e toda a sua pasta (jogo, mods, saves). Irreversível.</summary>
    public void Delete(MinecraftInstance instance)
    {
        var dir = LauncherPaths.InstanceDir(instance.Id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    /// <summary>
    ///     Indica se a instância já tem ficheiros de jogo instalados — verifica o
    ///     disco (pasta <c>versions</c> não vazia) em vez de confiar numa flag, para
    ///     refletir a realidade mesmo que o utilizador apague ficheiros à mão.
    /// </summary>
    public bool IsInstalled(MinecraftInstance instance)
    {
        var versionsDir = Path.Combine(LauncherPaths.InstanceGameDir(instance.Id), "versions");
        return Directory.Exists(versionsDir) && Directory.EnumerateDirectories(versionsDir).Any();
    }
}
