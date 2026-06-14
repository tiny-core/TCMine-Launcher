using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionLabel))]
    [NotifyPropertyChangedFor(nameof(UpdateAvailable))]
    private string? _installedVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionLabel))]
    [NotifyPropertyChangedFor(nameof(UpdateAvailable))]
    private string? _latestVersion;

    /// <summary>Painel de escolha de versão aberto (carrega as versões on-demand).</summary>
    [ObservableProperty] private bool _isChangingVersion;

    [ObservableProperty] private bool _versionsLoading;

    /// <summary>Versão escolhida no dropdown (aplicada no botão "Aplicar").</summary>
    [ObservableProperty] private ModVersionOption? _selectedVersion;

    public ModSearchItem(int modId, string name, string summary, bool isSelected,
        string? logoUrl = null, string? installedVersion = null)
    {
        ModId = modId;
        Name = name;
        Summary = summary;
        _isSelected = isSelected;
        LogoUrl = logoUrl;
        _installedVersion = installedVersion;
    }

    public int ModId { get; }
    public string Name { get; }
    public string Summary { get; }
    public string? LogoUrl { get; }

    /// <summary>Versões compatíveis disponíveis (carregadas on-demand do CurseForge).</summary>
    public ObservableCollection<ModVersionOption> Versions { get; } = new();

    public string ActionLabel => IsSelected ? "Remover" : "+ Adicionar";

    /// <summary>Há uma versão mais recente (preenchido pela verificação de atualizações).</summary>
    public bool UpdateAvailable => !string.IsNullOrEmpty(LatestVersion);

    /// <summary>Versão para a UI: "instalada → nova" se houver update, senão a instalada.</summary>
    public string VersionLabel => UpdateAvailable
        ? $"{InstalledVersion} → {LatestVersion}"
        : InstalledVersion ?? string.Empty;
}

/// <summary>Uma versão (ficheiro) disponível de um mod, para o dropdown de versões.</summary>
public sealed class ModVersionOption
{
    public ModVersionOption(CurseForgeFile file)
    {
        File = file;
        Label = string.IsNullOrWhiteSpace(file.DisplayName) ? file.FileName : file.DisplayName;
    }

    public CurseForgeFile File { get; }
    public string Label { get; }
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

    [ObservableProperty] private bool _isCheckingUpdates;
    [ObservableProperty] private bool _isSearching;

    [ObservableProperty] private string _query = string.Empty;
    private CancellationTokenSource? _searchCts;
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

        // Mostra já os mods atuais (removíveis) com a sua imagem e versão guardadas.
        foreach (var entry in _selected)
            Results.Add(new ModSearchItem(entry.ModId, entry.Name, string.Empty, true, entry.LogoUrl,
                entry.Version));

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
                var sel = _selected.FirstOrDefault(m => m.ModId == mod.Id);
                Results.Add(new ModSearchItem(
                    mod.Id, mod.Name, mod.Summary, sel is not null,
                    mod.Logo?.ThumbnailUrl, sel?.Version));
                shown.Add(mod.Id);
            }

            // Mantém visíveis os mods já selecionados que não vieram nos resultados.
            foreach (var entry in _selected.Where(e => !shown.Contains(e.ModId)))
                Results.Insert(0, new ModSearchItem(entry.ModId, entry.Name, string.Empty, true,
                    entry.LogoUrl, entry.Version));

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
            item.LatestVersion = null;
            _onChanged?.Invoke();
            return;
        }

        var gameVersion = _gameVersion() ?? string.Empty;
        try
        {
            var before = _selected.Count;
            var added = await AddWithDependenciesAsync(item.ModId, item.Name, item.LogoUrl, gameVersion,
                new HashSet<int>());
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
            Version = file.DisplayName,
            FileName = file.FileName,
            DownloadUrl = file.DownloadUrl,
            Sha1 = file.Sha1,
            LogoUrl = logoUrl
        });
        EnsureResultItem(modId, name, logoUrl, file.DisplayName);

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
    private void EnsureResultItem(int modId, string name, string? logoUrl = null, string? version = null)
    {
        var existing = Results.FirstOrDefault(r => r.ModId == modId);
        if (existing is not null)
        {
            existing.IsSelected = true;
            if (version is not null) existing.InstalledVersion = version;
            return;
        }

        Results.Insert(0, new ModSearchItem(modId, name, string.Empty, true, logoUrl, version));
    }

    /// <summary>
    ///     Verifica, para cada mod selecionado, se há um ficheiro mais recente compatível
    ///     (compara o FileId). Os updates ficam pendentes até o utilizador clicar "Atualizar".
    /// </summary>
    /// <summary>
    ///     Verifica, para cada mod selecionado, se há um ficheiro mais recente compatível
    ///     (compara o FileId) e mostra a versão nova como dica. NÃO aplica nada — a
    ///     instalação fica a cargo do dropdown de versão + "Aplicar".
    /// </summary>
    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        if (!_client.IsConfigured || _selected.Count == 0) return;

        IsCheckingUpdates = true;
        StatusMessage = null;
        var gameVersion = _gameVersion() ?? string.Empty;
        var found = 0;

        try
        {
            foreach (var entry in _selected.ToList())
            {
                CurseForgeFile? file;
                try { file = await _client.GetBestFileAsync(entry.ModId, gameVersion); }
                catch { continue; }

                if (file is null || file.Id == entry.FileId) continue;

                var item = Results.FirstOrDefault(r => r.ModId == entry.ModId);
                if (item is not null) item.LatestVersion = file.DisplayName;
                found++;
            }

            StatusMessage = found == 0
                ? "Todos os mods estão atualizados."
                : $"{found} atualização(ões) disponível(eis) — usa o campo de versão + Aplicar.";
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    /// <summary>
    ///     Abre o painel de versões de um mod e carrega (on-demand, uma só chamada em
    ///     cache) a lista de ficheiros compatíveis para escolher — atualizar ou rollback.
    /// </summary>
    [RelayCommand]
    private async Task ChangeVersionAsync(ModSearchItem item)
    {
        var entry = _selected.FirstOrDefault(m => m.ModId == item.ModId);
        if (entry is null) return;

        item.IsChangingVersion = true;

        if (item.Versions.Count > 0)
        {
            item.SelectedVersion ??= item.Versions.FirstOrDefault(v => v.File.Id == entry.FileId);
            return;
        }

        item.VersionsLoading = true;
        try
        {
            var files = await _client.GetFilesAsync(entry.ModId, _gameVersion() ?? string.Empty);
            item.Versions.Clear();
            foreach (var f in files.Where(f => !string.IsNullOrEmpty(f.DownloadUrl)))
                item.Versions.Add(new ModVersionOption(f));

            // Seleciona a versão instalada atual (se estiver na lista).
            item.SelectedVersion = item.Versions.FirstOrDefault(v => v.File.Id == entry.FileId)
                                   ?? item.Versions.FirstOrDefault();

            if (item.Versions.Count == 0)
                StatusMessage = $"'{item.Name}' não tem versões compatíveis com a versão do jogo.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao obter versões: " + ex.Message;
        }
        finally
        {
            item.VersionsLoading = false;
        }
    }

    /// <summary>Instala a versão escolhida no dropdown (atualizar OU rollback, por FileId).</summary>
    [RelayCommand]
    private void ApplyVersion(ModSearchItem item)
    {
        var file = item.SelectedVersion?.File;
        var entry = _selected.FirstOrDefault(m => m.ModId == item.ModId);
        if (file is null || entry is null) return;

        item.IsChangingVersion = false;

        if (file.Id == entry.FileId)
        {
            StatusMessage = $"'{item.Name}' já está nessa versão.";
            return;
        }

        entry.FileId = file.Id;
        entry.FileName = file.FileName;
        entry.DownloadUrl = file.DownloadUrl;
        entry.Sha1 = file.Sha1;
        entry.Version = file.DisplayName;

        item.InstalledVersion = file.DisplayName;
        item.LatestVersion = null;
        StatusMessage = $"'{item.Name}' → {file.DisplayName}";
        _onChanged?.Invoke();
    }

    /// <summary>Fecha o painel de versões sem aplicar.</summary>
    [RelayCommand]
    private void CancelVersion(ModSearchItem item)
    {
        item.IsChangingVersion = false;
    }

    /// <summary>Abre a página do mod no CurseForge (para ver as versões disponíveis).</summary>
    [RelayCommand]
    private void OpenModPage(ModSearchItem item)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                $"https://www.curseforge.com/projects/{item.ModId}") { UseShellExecute = true });
        }
        catch
        {
            /* sem browser disponível — ignora */
        }
    }
}