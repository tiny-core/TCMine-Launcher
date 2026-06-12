using System.Text.Json.Serialization;

namespace TCMine_Launcher.Models;

/// <summary>
///     Configuração de um perfil de lançamento.
///     Representa o QUÊ vai ser lançado — não COMO é mostrado no ecrã.
///     Pode ser guardado em arquivo, carregado, comparado, etc.
/// </summary>
public class GameProfile
{
    public string Name { get; set; } = "TCMine";

    // ── Versões ───────────────────────────────────────────────────

    public string MinecraftVersion { get; set; } = "1.21.1";
    public string NeoForgeVersion { get; set; } = "21.1.172";

    //    // ── JVM ───────────────────────────────────────────────────────
    public int AllocatedRamMb { get; set; } = 4096;
    public string? JavaPath { get; set; } // null = auto-detectar

    //    // ── Lógica de domínio pura (calculada — não persistir) ────────
    /// <summary>Descrição compacta do perfil para listas/menus.</summary>
    [JsonIgnore]
    public string DisplayName => $"NeoForge {NeoForgeVersion} — MC {MinecraftVersion}";

    /// <summary>Argumentos de memória JVM calculados a partir do modelo.</summary>
    [JsonIgnore]
    public string JvmMemoryArgs => $"-Xms512m -Xmx{AllocatedRamMb}m";
}