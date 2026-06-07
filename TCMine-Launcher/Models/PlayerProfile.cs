using System;

namespace TCMine_Launcher.Models;

/// <summary>
///     Perfil do jogador — dados puros, zero dependências de UI ou Avalonia.
///     Pode ser serializado para JSON, carregado de disco, passado para testes unitários.
/// </summary>
public class PlayerProfile
{
    public string Name { get; set; } = "Steve";

    public AccountType AccountType { get; set; } = AccountType.Offline;

    /// <summary>Texto descritivo do tipo de conta.</summary>
    public string AccountLabel => AccountType switch
    {
        AccountType.Microsoft => "Conta Microsoft",
        _ => "Conta local · Offline"
    };

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