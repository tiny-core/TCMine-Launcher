using System.ComponentModel.DataAnnotations;

namespace TCMine_Server.Data.Entities;

/// <summary>
///     Metadados de uma release do launcher. Os artefactos (Setup.exe, .nupkg,
///     releases.&lt;channel&gt;.json) ficam no <c>UPDATES_DIR</c> e são servidos
///     pelo Velopack em <c>/updates</c>; esta entidade guarda o histórico/changelog.
/// </summary>
public class ReleaseEntity
{
    public int Id { get; set; }

    [MaxLength(40)] public string Version { get; set; } = string.Empty;

    [MaxLength(20)] public string Channel { get; set; } = "win";

    [MaxLength(4000)] public string Notes { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Nomes dos ficheiros carregados para o UPDATES_DIR (separados por '\n').</summary>
    [MaxLength(4000)]
    public string Files { get; set; } = string.Empty;
}