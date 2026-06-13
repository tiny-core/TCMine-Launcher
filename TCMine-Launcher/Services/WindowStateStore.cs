using System.IO;
using System.Text.Json;

namespace TCMine_Launcher.Services;

/// <summary>Tamanho/posição da janela, persistidos entre execuções.</summary>
public class LauncherWindowState
{
    public double Width { get; set; }
    public double Height { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public bool Maximized { get; set; }
    public bool HasPosition { get; set; }
}

/// <summary>Lê/grava o estado da janela em <c>window.json</c>.</summary>
public static class WindowStateStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private static string FilePath => Path.Combine(LauncherPaths.Root, "window.json");

    public static LauncherWindowState? Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<LauncherWindowState>(File.ReadAllText(FilePath), Options);
        }
        catch
        {
            // ignora ficheiro inválido
        }
        return null;
    }

    public static void Save(LauncherWindowState state)
    {
        try
        {
            LauncherPaths.EnsureRoot();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state, Options));
        }
        catch
        {
            // best-effort
        }
    }
}
