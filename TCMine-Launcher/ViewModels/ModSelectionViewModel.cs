using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Item da lista de mods: dados do CurseForge + estado de seleção. O botão
///     mostra "Adicionar" ou "Remover" consoante <see cref="IsSelected" />.
/// </summary>
public partial class ModSearchItem : ViewModelBase
{
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ActionLabel))]
    private bool _isSelected;

    public ModSearchItem(int modId, string name, string summary, bool isSelected, string? logoUrl = null)
    {
        ModId = modId;
        Name = name;
        Summary = summary;
        _isSelected = isSelected;
        LogoUrl = logoUrl;
    }

    public int ModId { get; }
    public string Name { get; }
    public string Summary { get; }
    public string? LogoUrl { get; }

    public string ActionLabel => IsSelected ? "Remover" : "+ Adicionar";
}

/// <summary>
///     Componente reutilizável de pesquisa/seleção de mods do CurseForge.
///     Uma única lista: cada mod tem um botão que alterna Adicionar/Remover.
///     O host fornece a coleção alvo, a versão de jogo e um callback para persistir.
/// </summary>
public partial class ModSelectionViewModel : ViewModelBase
{
    private readonly CurseForgeClient _client;
    private readonly Func<string?> _gameVersion;
    private readonly Action? _onChanged;
    private readonly ObservableCollection<ModEntry> _selected;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string? _statusMessage;

    public ModSelectionViewModel(
        CurseForgeClient client,
        ObservableCollection<ModEntry> selected,
        Func<string?> gameVersion,
        Action? onChanged = null)
    {
        _client = client;
        _selected = selected;
        _gameVersion = gameVersion;
        _onChanged = onChanged;

        // Mostra já os mods atuais (removíveis) com a sua imagem guardada.
        foreach (var entry in _selected)
            Results.Add(new ModSearchItem(entry.ModId, entry.Name, string.Empty, true, entry.LogoUrl));

        if (!client.IsConfigured)
            StatusMessage = "Proxy do CurseForge não configurado (Definições).";
    }

    /// <summary>Lista única (resultados + selecionados atuais).</summary>
    public ObservableCollection<ModSearchItem> Results { get; } = new();

    public bool IsConfigured => _client.IsConfigured;

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (!_client.IsConfigured)
        {
            StatusMessage = "Proxy do CurseForge não configurado (Definições).";
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        IsSearching = true;
        StatusMessage = null;

        try
        {
            var mods = await _client.SearchAsync(Query, 0, _searchCts.Token);

            Results.Clear();
            var shown = new HashSet<int>();
            foreach (var mod in mods)
            {
                Results.Add(new ModSearchItem(
                    mod.Id, mod.Name, mod.Summary, _selected.Any(m => m.ModId == mod.Id),
                    mod.Logo?.ThumbnailUrl));
                shown.Add(mod.Id);
            }

            // Mantém visíveis os mods já selecionados que não vieram nos resultados.
            foreach (var entry in _selected.Where(e => !shown.Contains(e.ModId)))
                Results.Insert(0, new ModSearchItem(entry.ModId, entry.Name, string.Empty, true));

            if (Results.Count == 0) StatusMessage = "Sem resultados.";
        }
        catch (OperationCanceledException)
        {
            // pesquisa substituída por outra — ignora
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro na pesquisa: " + ex.Message;
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task ToggleAsync(ModSearchItem item)
    {
        if (item.IsSelected)
        {
            var existing = _selected.FirstOrDefault(m => m.ModId == item.ModId);
            if (existing is not null) _selected.Remove(existing);
            item.IsSelected = false;
            _onChanged?.Invoke();
            return;
        }

        var gameVersion = _gameVersion() ?? string.Empty;
        try
        {
            var before = _selected.Count;
            var added = await AddWithDependenciesAsync(item.ModId, item.Name, item.LogoUrl, gameVersion, new HashSet<int>());
            if (!added) return;

            item.IsSelected = true;
            var deps = _selected.Count - before - 1;
            if (deps > 0) StatusMessage = $"Adicionado com {deps} dependência(s).";
            _onChanged?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao adicionar: " + ex.Message;
        }
    }

    /// <summary>Adiciona um mod e, recursivamente, as suas dependências obrigatórias.</summary>
    private async Task<bool> AddWithDependenciesAsync(
        int modId, string name, string? logoUrl, string gameVersion, HashSet<int> visited)
    {
        if (!visited.Add(modId)) return true;
        if (_selected.Any(m => m.ModId == modId)) return true;

        var file = await _client.GetBestFileAsync(modId, gameVersion);
        if (file is null)
        {
            StatusMessage = $"'{name}' não tem versão compatível com {gameVersion}.";
            return false;
        }

        _selected.Add(new ModEntry
        {
            ModId = modId,
            FileId = file.Id,
            Name = name,
            FileName = file.FileName,
            DownloadUrl = file.DownloadUrl,
            Sha1 = file.Sha1,
            LogoUrl = logoUrl
        });
        EnsureResultItem(modId, name, logoUrl);

        if (file.Dependencies is not null)
            foreach (var dep in file.Dependencies.Where(d => d.RelationType == 3))
            {
                if (_selected.Any(m => m.ModId == dep.ModId)) continue;
                var depMod = await _client.GetModAsync(dep.ModId);
                await AddWithDependenciesAsync(
                    dep.ModId, depMod?.Name ?? $"Mod {dep.ModId}", depMod?.Logo?.ThumbnailUrl,
                    gameVersion, visited);
            }

        return true;
    }

    /// <summary>Garante que um mod aparece na lista (marcado), inserindo-o se preciso.</summary>
    private void EnsureResultItem(int modId, string name, string? logoUrl = null)
    {
        var existing = Results.FirstOrDefault(r => r.ModId == modId);
        if (existing is not null)
        {
            existing.IsSelected = true;
            return;
        }
        Results.Insert(0, new ModSearchItem(modId, name, string.Empty, true, logoUrl));
    }
}
