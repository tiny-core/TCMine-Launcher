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
    UpdateAvailable
}

/// <summary>Item da lista de modpacks: manifesto + estado de instalação local.</summary>
public partial class ModpackListItem : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstallLabel))]
    [NotifyPropertyChangedFor(nameof(UpdateAvailable))]
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

    public string InstallLabel => State switch
    {
        ModpackInstallState.UpdateAvailable => "Atualizar",
        ModpackInstallState.Installed => "Reinstalar",
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
