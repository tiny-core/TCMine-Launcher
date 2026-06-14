using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    // Cache de instâncias já instaladas: uma vez instalada, mantém-se instalada até
    // ser eliminada — por isso só memoizamos positivos (evita enumerar o disco a cada
    // refresh da UI). Negativos não são cacheados, para refletir um install recente.
    private readonly HashSet<string> _installedCache = new();

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
        _installedCache.Remove(instance.Id);
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
        if (_installedCache.Contains(instance.Id)) return true;

        var versionsDir = Path.Combine(LauncherPaths.InstanceGameDir(instance.Id), "versions");
        var installed = Directory.Exists(versionsDir) && Directory.EnumerateDirectories(versionsDir).Any();
        if (installed) _installedCache.Add(instance.Id);
        return installed;
    }

    /// <summary>Exporta a instância (config + jogo) para um ficheiro zip.</summary>
    public void Export(MinecraftInstance instance, string zipPath)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(LauncherPaths.InstanceDir(instance.Id), zipPath);
    }

    /// <summary>
    ///     Importa uma instância de um zip: extrai para uma nova pasta, atribui um
    ///     novo Id e torna-a Manual (editável). Devolve a instância criada.
    /// </summary>
    public MinecraftInstance Import(string zipPath)
    {
        var newId = Guid.NewGuid().ToString("N");
        var dir = LauncherPaths.InstanceDir(newId);
        Directory.CreateDirectory(dir);
        ZipFile.ExtractToDirectory(zipPath, dir);

        var configFile = Path.Combine(dir, "instance.json");
        if (!File.Exists(configFile))
        {
            Directory.Delete(dir, true);
            throw new InvalidDataException("Zip inválido: não contém instance.json.");
        }

        var instance = JsonSerializer.Deserialize<MinecraftInstance>(
                           File.ReadAllText(configFile), Options)
                       ?? throw new InvalidDataException("instance.json inválido.");

        instance.Id = newId;
        instance.Source = InstanceSource.Manual;
        instance.ModpackId = null;
        instance.ManifestVersion = null;
        if (!instance.Name.Contains("(importada)"))
            instance.Name += " (importada)";

        Save(instance);
        return instance;
    }
}