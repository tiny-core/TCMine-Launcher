using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>Estado de instalação de um modpack relativamente às instâncias locais.</summary>
public enum ModpackInstallState
{
    NotInstalled,
    Installed,
    UpdateAvailable,

    /// <summary>Instalado, mas o modpack já não existe/foi despublicado no servidor.</summary>
    Discontinued
}

/// <summary>Item da lista de modpacks: manifesto + estado de instalação local.</summary>
public partial class ModpackListItem : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallLabel))]
    [NotifyPropertyChangedFor(nameof(UpdateAvailable))]
    [NotifyPropertyChangedFor(nameof(IsDiscontinued))]
    private ModpackInstallState _state;

    public ModpackListItem(ModpackManifest manifest, ModpackInstallState state)
    {
        Manifest = manifest;
        _state = state;
    }

    public ModpackManifest Manifest { get; }

    public string Name => Manifest.Name;
    public string VersionSummary => Manifest.VersionSummary;
    public string ModsSummary => Manifest.ModsSummary;
    public bool HasServer => Manifest.HasServer;
    public string? Description => Manifest.Description;

    public bool UpdateAvailable => State == ModpackInstallState.UpdateAvailable;

    /// <summary>Modpack descontinuado (instalado, mas já não está no servidor).</summary>
    public bool IsDiscontinued => State == ModpackInstallState.Discontinued;

    public string InstallLabel => State switch
    {
        ModpackInstallState.UpdateAvailable => "Atualizar",
        ModpackInstallState.Installed => "Reinstalar",
        ModpackInstallState.Discontinued => "Indisponível",
        _ => "Instalar"
    };
}

/// <summary>
///     Página "Modpacks": lista os modpacks oficiais do servidor TCMine, indica se já
///     estão instalados / têm atualização, e permite instalar/atualizar.
/// </summary>
public partial class ModpacksPageViewModel : ViewModelBase
{
    private readonly ManifestService _manifest;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    public ModpacksPageViewModel(MainWindowViewModel shell, ManifestService manifest)
    {
        _shell = shell;
        _manifest = manifest;
    }

    public ObservableCollection<ModpackListItem> Modpacks { get; } = new();

    /// <summary>Instalar/atualizar fica bloqueado enquanto há um jogo aberto.</summary>
    public bool CanInteract => !_shell.IsGameRunning;

    /// <summary>Chamado pelo shell quando o jogo abre/fecha.</summary>
    public void NotifyGameRunningChanged()
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    /// <summary>Recarrega a lista (e recalcula estados) sempre que a página é mostrada.</summary>
    public void Begin()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!_manifest.IsConfigured)
        {
            StatusMessage = "Configura o URL do servidor TCMine nas Definições.";
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        try
        {
            var list = await _manifest.GetModpacksAsync();
            Modpacks.Clear();
            foreach (var modpack in list)
                Modpacks.Add(new ModpackListItem(modpack, ResolveState(modpack)));

            // Junta as instâncias oficiais cujo modpack já não vem do servidor
            // (despublicado/removido) — mostra-as como descontinuadas a partir do
            // snapshot local, para não desaparecerem do catálogo.
            var catalogIds = list.Select(m => m.Id).ToHashSet();
            foreach (var instance in _shell.Instances)
            {
                if (!instance.IsOfficial || string.IsNullOrEmpty(instance.ModpackId)) continue;
                if (catalogIds.Contains(instance.ModpackId)) continue;

                Modpacks.Add(new ModpackListItem(
                    ManifestFromInstance(instance), ModpackInstallState.Discontinued));
            }

            if (Modpacks.Count == 0) StatusMessage = "Nenhum modpack disponível no servidor.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao carregar modpacks: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Constrói um manifesto a partir do snapshot local de uma instância oficial.</summary>
    private static ModpackManifest ManifestFromInstance(MinecraftInstance instance) => new()
    {
        Id = instance.ModpackId ?? string.Empty,
        Name = instance.Name,
        Version = instance.ManifestVersion ?? string.Empty,
        Minecraft = instance.MinecraftVersion,
        Neoforge = instance.NeoForgeVersion,
        Description = instance.Description,
        Mods = instance.Mods,
        Servers = instance.Servers
    };

    private ModpackInstallState ResolveState(ModpackManifest manifest)
    {
        var existing = _shell.Instances.FirstOrDefault(i => i.ModpackId == manifest.Id);
        if (existing is null) return ModpackInstallState.NotInstalled;
        return existing.ManifestVersion != manifest.Version
            ? ModpackInstallState.UpdateAvailable
            : ModpackInstallState.Installed;
    }

    [RelayCommand]
    private async Task InstallAsync(ModpackListItem item)
    {
        // Descontinuado: não há manifesto no servidor para (re)instalar.
        if (item.State == ModpackInstallState.Discontinued) return;

        try
        {
            var full = await _manifest.GetManifestAsync(item.Manifest.Id) ?? item.Manifest;
            _shell.InstallFromManifest(full);
            _shell.NavigateToHome();
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao instalar: " + ex.Message;
        }
    }
}