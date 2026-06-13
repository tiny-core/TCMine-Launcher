using System.Collections.Generic;

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
}
