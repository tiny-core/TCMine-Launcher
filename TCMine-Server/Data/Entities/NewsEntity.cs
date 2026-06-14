using System.ComponentModel.DataAnnotations;

namespace TCMine_Server.Data.Entities;

/// <summary>Uma novidade mostrada na aba "Novidades" do launcher.</summary>
public class NewsEntity
{
    public int Id { get; set; }

    [MaxLength(40)] public string Tag { get; set; } = string.Empty;

    [MaxLength(200)] public string Title { get; set; } = string.Empty;

    [MaxLength(1000)] public string Summary { get; set; } = string.Empty;

    /// <summary>Data de publicação (usada para ordenar e formatar o campo "date" público).</summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Só as publicadas aparecem no endpoint público.</summary>
    public bool IsPublished { get; set; } = true;
}