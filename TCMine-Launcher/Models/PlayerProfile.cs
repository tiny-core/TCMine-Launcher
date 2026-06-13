using System;

namespace TCMine_Launcher.Models;

/// <summary>
///     Perfil do jogador — dados puros, zero dependências de UI ou Avalonia.
///     Pode ser serializado para JSON, carregado de disco, passado para testes unitários.
/// </summary>
public class PlayerProfile
{
    public string Name { get; set; } = "Steve";

    /// <summary>UUID do Minecraft (vazio em contas offline sem sessão).</summary>
    public string Uuid { get; set; } = string.Empty;

    public AccountType AccountType { get; set; } = AccountType.Offline;

    /// <summary>Texto descritivo do tipo de conta.</summary>
    public string AccountLabel => AccountType switch
    {
        AccountType.Microsoft => "Conta Microsoft",
        _ => "Conta local · Offline"
    };

    /// <summary>URL da cabeça da skin (mc-heads). Null em contas sem UUID.</summary>
    public string? HeadUrl =>
        string.IsNullOrEmpty(Uuid) ? null : $"https://mc-heads.net/avatar/{Uuid}/128";

    // ── Lógica de domínio pura (não é formatação de UI) ──────────
    /// <summary>Até 2 iniciais em maiúsculas para o avatar circular.</summary>
    public string ComputeInitials()
    {
        return Name.Length > 0 ? Name[..Math.Min(2, Name.Length)].ToUpper() : "??";
    }
}

public enum AccountType
{
    Offline,
    Microsoft
}