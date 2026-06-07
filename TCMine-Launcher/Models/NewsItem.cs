namespace TCMine_Launcher.Models;

/// <summary>
///     Item de novidade/changelog — dados puros para a tela de Novidades.
/// </summary>
public class NewsItem
{
    public string Tag { get; set; } = "Notícia";
    public string Title { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
