using System.Text.Json.Serialization;

namespace TCMine_Launcher.Models;

/// <summary>
///     Um servidor de Minecraft associado a um modpack/instância. Escrito no
///     <c>servers.dat</c> para aparecer na lista multijogador do jogo.
/// </summary>
public class ServerEntry
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; } = 25565;

    /// <summary>IP no formato do Minecraft (inclui porta só se não for a default).</summary>
    [JsonIgnore]
    public string Ip => Port == 25565 ? Address : $"{Address}:{Port}";
}
