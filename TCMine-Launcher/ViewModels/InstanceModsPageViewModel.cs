using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Janela de gestão de uma instância: editar nome, versões, servidores e mods.
///     As alterações ficam só em memória (rascunho) e são gravadas no disco apenas
///     ao clicar <b>Concluído</b>. Fechar a janela sem concluir cancela tudo (uma
///     instância nova nem chega a ser criada).
/// </summary>
public partial class InstanceModsPageViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private readonly VersionService _versions = new();
    private MinecraftInstance? _instance;
    [ObservableProperty] private bool _isLoadingNeoForge;
    private bool _isNew;
    [ObservableProperty] private string _javaPathOverride = string.Empty;
    private bool _loading;

    [ObservableProperty] private ModSelectionViewModel? _modSelection;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _newServerAddress = string.Empty;

    // Campos do formulário "adicionar servidor"
    [ObservableProperty] private string _newServerName = string.Empty;
    [ObservableProperty] private string _newServerPort = "25565";
    [ObservableProperty] private string? _selectedMinecraftVersion;
    private ObservableCollection<ModEntry>? _selectedMods;
    [ObservableProperty] private string? _selectedNeoForgeVersion;

    public InstanceModsPageViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        Servers.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ServerCount));
    }

    /// <summary>Servidores associados à instância (editáveis em memória).</summary>
    public ObservableCollection<ServerEntry> Servers { get; } = new();

    /// <summary>Nº de servidores configurados (para o botão).</summary>
    public int ServerCount => Servers.Count;

    /// <summary>Nº de mods selecionados (para o botão).</summary>
    public int ModCount => _selectedMods?.Count ?? 0;

    [RelayCommand]
    private void OpenServers()
    {
        _shell.ShowServerList(this);
    }

    public ObservableCollection<string> MinecraftVersions { get; } = new();
    public ObservableCollection<string> NeoForgeVersions { get; } = new();

    [RelayCommand]
    private void OpenMods()
    {
        if (ModSelection is not null) _shell.ShowModSelection(ModSelection);
    }

    /// <summary>Pedido para fechar a janela (ligado pela View).</summary>
    public event Action? CloseRequested;

    public void Begin(MinecraftInstance instance, bool isNew)
    {
        _instance = instance;
        _isNew = isNew;
        _loading = true;
        Name = instance.Name;
        JavaPathOverride = instance.JavaPathOverride ?? string.Empty;

        // O callback atualiza só a lista em memória (gravação acontece em Concluído).
        var selected = new ObservableCollection<ModEntry>(instance.Mods);
        _selectedMods = selected;
        selected.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ModCount));
        ModSelection = new ModSelectionViewModel(
            _shell.CurseForge,
            selected,
            () => instance.MinecraftVersion,
            () => instance.Mods = selected.ToList());
        OnPropertyChanged(nameof(ModCount));

        Servers.Clear();
        foreach (var server in instance.Servers) Servers.Add(server);

        _loading = false;
        _ = LoadVersionsAsync(instance);
    }

    [RelayCommand]
    private void AddServer()
    {
        if (_instance is null || string.IsNullOrWhiteSpace(NewServerAddress)) return;

        if (!int.TryParse(NewServerPort?.Trim(), out var port)) port = 25565;

        Servers.Add(new ServerEntry
        {
            Name = string.IsNullOrWhiteSpace(NewServerName) ? NewServerAddress.Trim() : NewServerName.Trim(),
            Address = NewServerAddress.Trim(),
            Port = port
        });
        _instance.Servers = Servers.ToList();

        NewServerName = string.Empty;
        NewServerAddress = string.Empty;
        NewServerPort = "25565";
    }

    [RelayCommand]
    private void RemoveServer(ServerEntry server)
    {
        if (_instance is null) return;
        Servers.Remove(server);
        _instance.Servers = Servers.ToList();
    }

    private async Task LoadVersionsAsync(MinecraftInstance instance)
    {
        _loading = true;
        try
        {
            var releases = await _versions.GetMinecraftReleasesAsync();
            Fill(MinecraftVersions, releases.Count > 0 ? releases : new[] { instance.MinecraftVersion });
        }
        catch
        {
            Fill(MinecraftVersions, new[] { instance.MinecraftVersion });
        }

        if (!MinecraftVersions.Contains(instance.MinecraftVersion))
            MinecraftVersions.Insert(0, instance.MinecraftVersion);

        SelectedMinecraftVersion = instance.MinecraftVersion;
        _loading = false;

        await LoadNeoForgeAsync(instance.MinecraftVersion, instance.NeoForgeVersion);
    }

    private async Task LoadNeoForgeAsync(string mcVersion, string? preferred)
    {
        IsLoadingNeoForge = true;
        try
        {
            var list = await _versions.GetNeoForgeVersionsAsync(mcVersion);
            Fill(NeoForgeVersions, list);
        }
        catch
        {
            NeoForgeVersions.Clear();
        }
        finally
        {
            IsLoadingNeoForge = false;
        }

        if (preferred is not null && !NeoForgeVersions.Contains(preferred))
            NeoForgeVersions.Insert(0, preferred);

        var choice = preferred ?? NeoForgeVersions.FirstOrDefault();

        _loading = true;
        SelectedNeoForgeVersion = choice;
        _loading = false;

        if (_instance is not null && choice is not null)
            _instance.NeoForgeVersion = choice; // só em memória
    }

    partial void OnNameChanged(string value)
    {
        if (_loading || _instance is null) return;
        _instance.Name = string.IsNullOrWhiteSpace(value) ? _instance.Name : value.Trim();
    }

    partial void OnSelectedMinecraftVersionChanged(string? value)
    {
        if (_loading || _instance is null || value is null) return;
        _instance.MinecraftVersion = value;
        _ = LoadNeoForgeAsync(value, null);
    }

    partial void OnSelectedNeoForgeVersionChanged(string? value)
    {
        if (_loading || _instance is null || value is null) return;
        _instance.NeoForgeVersion = value;
    }

    partial void OnJavaPathOverrideChanged(string value)
    {
        if (_loading || _instance is null) return;
        _instance.JavaPathOverride = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void Fill(ObservableCollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    /// <summary>Grava o rascunho no disco e fecha. Único momento em que se escreve.</summary>
    [RelayCommand]
    private void Conclude()
    {
        if (_instance is not null)
            _shell.CommitInstance(_instance, _isNew);
        CloseRequested?.Invoke();
    }

    /// <summary>Cancela: fecha sem gravar (a instância nova é descartada).</summary>
    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke();
    }
}