using System.IO;
using System.Text.Json;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Persiste as definições globais do launcher em <c>settings.json</c>.
///     Reutiliza o Model puro <see cref="GameProfile" /> (já é serializável) em vez
///     de inventar um DTO à parte. Escrita atómica (.tmp + move) para nunca deixar
///     um JSON meio-escrito se a app fechar a meio.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Carrega o perfil do disco; devolve os defaults se não existir/falhar.</summary>
    public GameProfile Load()
    {
        try
        {
            if (File.Exists(LauncherPaths.SettingsFile))
            {
                var json = File.ReadAllText(LauncherPaths.SettingsFile);
                var profile = JsonSerializer.Deserialize<GameProfile>(json, Options);
                if (profile is not null) return profile;
            }
        }
        catch
        {
            // Ficheiro corrompido/ilegível — segue com os defaults.
        }

        return new GameProfile();
    }

    /// <summary>Grava o perfil no disco de forma atómica. Best-effort.</summary>
    public void Save(GameProfile profile)
    {
        try
        {
            LauncherPaths.EnsureRoot();
            var json = JsonSerializer.Serialize(profile, Options);
            var tmp = LauncherPaths.SettingsFile + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, LauncherPaths.SettingsFile, true);
        }
        catch
        {
            // Falha a gravar não deve derrubar a app.
        }
    }
}