using System.Collections.ObjectModel;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Novidades": feed de notícias/changelog do launcher e do modpack.
/// </summary>
public class NewsPageViewModel : ViewModelBase
{
    public NewsPageViewModel()
    {
        News = new ObservableCollection<NewsItem>
        {
            new()
            {
                Tag = "MODPACK",
                Title = "TCMine Modpack 1.0.0 disponível",
                Date = "07 jun 2026",
                Summary = "Primeira versão pública do nosso pack custom, com mods de " +
                          "tecnologia, exploração e novos biomas."
            },
            new()
            {
                Tag = "LAUNCHER",
                Title = "Login com a Microsoft",
                Date = "05 jun 2026",
                Summary = "Agora podes entrar com a tua conta Microsoft para sincronizar " +
                          "o teu perfil e skins."
            },
            new()
            {
                Tag = "SERVIDOR",
                Title = "Servidor TCMine renovado",
                Date = "01 jun 2026",
                Summary = "Mapa novo, regras actualizadas e eventos semanais a começar já."
            }
        };
    }

    public ObservableCollection<NewsItem> News { get; }
}
