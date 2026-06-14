using System.ComponentModel.DataAnnotations;

namespace TCMine_Server.Data.Entities;

/// <summary>
///     Configurações do jogador (keybinds, shader/texturas selecionados, minimapa)
///     guardadas por <c>(Uuid, ModpackId)</c> como um zip. Permite repor as configs
///     quando o jogador entra noutro PC. Sem auth: a chave é o UUID do Minecraft —
///     são apenas settings de jogo (sem segredos). Last-write-wins por <see cref="UpdatedAt" />.
/// </summary>
public class PlayerConfigEntity
{
    /// <summary>UUID do Minecraft do jogador (parte da chave composta).</summary>
    [MaxLength(40)]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>Slug do modpack oficial a que estas configs pertencem (parte da chave).</summary>
    [MaxLength(80)]
    public string ModpackId { get; set; } = string.Empty;

    /// <summary>Zip dos ficheiros player-owned (relativos à pasta do jogo).</summary>
    public byte[] Data { get; set; } = [];

    public DateTime UpdatedAt { get; set; }
}