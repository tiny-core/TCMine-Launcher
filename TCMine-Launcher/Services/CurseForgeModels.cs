using System.Collections.Generic;
using System.Linq;

namespace TCMine_Launcher.Services;

/// <summary>
///     DTOs de desserialização das respostas do CurseForge (reencaminhadas pelo proxy
///     1:1, no mesmo formato de <c>api.curseforge.com</c>). Não são Models de domínio
///     — vivem na camada de serviços.
/// </summary>
public class CurseForgeListResponse<T>
{
    public List<T> Data { get; set; } = new();
}

public class CurseForgeSingleResponse<T>
{
    public T? Data { get; set; }
}

public class CurseForgeMod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public CurseForgeLogo? Logo { get; set; }
}

public class CurseForgeLogo
{
    public string? ThumbnailUrl { get; set; }
}

public class CurseForgeFile
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>URL do CDN; nulo quando a distribuição por terceiros é proibida.</summary>
    public string? DownloadUrl { get; set; }

    public List<CurseForgeHash>? Hashes { get; set; }
    public List<CurseForgeDependency>? Dependencies { get; set; }

    /// <summary>Hash SHA-1 (algo = 1) para verificar a integridade do download.</summary>
    public string? Sha1 => Hashes?.FirstOrDefault(h => h.Algo == 1)?.Value;
}

public class CurseForgeHash
{
    public string Value { get; set; } = string.Empty;
    public int Algo { get; set; }
}

public class CurseForgeDependency
{
    public int ModId { get; set; }

    /// <summary>3 = dependência obrigatória (required).</summary>
    public int RelationType { get; set; }
}
