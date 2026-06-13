using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Modpacks": lista os modpacks oficiais servidos pelo servidor TCMine
///     e permite instalá-los (cria/atualiza uma instância a partir do manifesto).
/// </summary>
public partial class ModpacksPageViewModel : ViewModelBase
{
    private readonly ManifestService _manifest;
    private readonly MainWindowViewModel _shell;
    private bool _loadedOnce;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    public ModpacksPageViewModel(MainWindowViewModel shell, ManifestService manifest)
    {
        _shell = shell;
        _manifest = manifest;
    }

    public ObservableCollection<ModpackManifest> Modpacks { get; } = new();

    /// <summary>Carrega a lista na primeira vez que a página é mostrada.</summary>
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
            foreach (var modpack in list) Modpacks.Add(modpack);
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

    [RelayCommand]
    private async Task InstallAsync(ModpackManifest summary)
    {
        try
        {
            // Vai buscar o manifesto completo (com mods + servidores).
            var full = await _manifest.GetManifestAsync(summary.Id) ?? summary;
            _shell.InstallFromManifest(full);
            _shell.NavigateToHome();
        }
        catch (Exception ex)
        {
            StatusMessage = "Erro ao instalar: " + ex.Message;
        }
    }
}
