using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Novidades": feed de notícias publicado pelo servidor TCMine (<c>/news</c>).
/// </summary>
public partial class NewsPageViewModel : ViewModelBase
{
    private readonly NewsService _news;

    [ObservableProperty] private bool _isLoading;
    private bool _loadedOnce;
    [ObservableProperty] private string? _statusMessage;

    public NewsPageViewModel(NewsService news)
    {
        _news = news;
    }

    public ObservableCollection<NewsItem> News { get; } = new();

    /// <summary>Carrega na primeira vez que a página é mostrada.</summary>
    public void Begin()
    {
        if (_loadedOnce) return;
        _loadedOnce = true;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!_news.IsConfigured)
        {
            StatusMessage = "Configura o URL do servidor TCMine nas Definições.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        try
        {
            var list = await _news.GetNewsAsync();
            News.Clear();
            foreach (var item in list) News.Add(item);
            if (News.Count == 0) StatusMessage = "Sem novidades.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao carregar novidades: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}