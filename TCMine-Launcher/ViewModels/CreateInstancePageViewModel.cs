using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página dedicada à criação de uma nova instância. Puxa as versões reais
///     (releases do Minecraft + NeoForge compatível) via <see cref="VersionService" />,
///     com fallback para uma lista estática se a rede falhar. A seleção de mods
///     chega na fase de integração com o CurseForge.
/// </summary>
public partial class CreateInstancePageViewModel : ViewModelBase
{
    private static readonly string[] FallbackMinecraft =
        { "1.21.4", "1.21.3", "1.21.1", "1.20.6", "1.20.4", "1.20.1" };

    private static readonly string[] FallbackNeoForge =
        { "21.1.172", "21.1.171", "21.1.170", "21.1.165", "21.1.160" };

    private readonly GameProfile _game;
    private readonly MainWindowViewModel _shell;
    private readonly VersionService _versions = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _name = "Nova Instância";

    [ObservableProperty] private bool _isLoadingMinecraft;
    [ObservableProperty] private bool _isLoadingNeoForge;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _selectedMinecraftVersion;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _selectedNeoForgeVersion;

    private readonly ObservableCollection<ModEntry> _pendingMods = new();

    public CreateInstancePageViewModel(GameProfile game, MainWindowViewModel shell)
    {
        _game = game;
        _shell = shell;

        ModSelection = new ModSelectionViewModel(
            shell.CurseForge, _pendingMods, () => SelectedMinecraftVersion);
        _pendingMods.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ModCount));
    }

    /// <summary>Nº de mods selecionados (para o botão).</summary>
    public int ModCount => _pendingMods.Count;

    [RelayCommand]
    private void OpenMods()
    {
        _shell.ShowModSelection(ModSelection);
    }

    public ObservableCollection<string> MinecraftVersions { get; } = new();
    public ObservableCollection<string> NeoForgeVersions { get; } = new();

    /// <summary>Seleção de mods inicial (anexada à instância ao criar).</summary>
    public ModSelectionViewModel ModSelection { get; }

    /// <summary>Prepara o formulário ao abrir a página (recarrega versões).</summary>
    public void Begin()
    {
        Name = "Nova Instância";
        _pendingMods.Clear();
        ModSelection.Results.Clear();
        _ = LoadMinecraftVersionsAsync();
    }

    private async Task LoadMinecraftVersionsAsync()
    {
        IsLoadingMinecraft = true;
        try
        {
            var list = await _versions.GetMinecraftReleasesAsync();
            FillReplacing(MinecraftVersions, list.Count > 0 ? list : FallbackMinecraft);
        }
        catch
        {
            FillReplacing(MinecraftVersions, FallbackMinecraft);
        }
        finally
        {
            IsLoadingMinecraft = false;
        }

        // Pré-seleciona o default global se existir na lista; senão o primeiro.
        SelectedMinecraftVersion = MinecraftVersions.Contains(_game.MinecraftVersion)
            ? _game.MinecraftVersion
            : MinecraftVersions.FirstOrDefault();
    }

    private async Task LoadNeoForgeVersionsAsync(string mcVersion)
    {
        IsLoadingNeoForge = true;
        try
        {
            var list = await _versions.GetNeoForgeVersionsAsync(mcVersion);
            FillReplacing(NeoForgeVersions, list.Count > 0 ? list : FallbackNeoForge);
        }
        catch
        {
            FillReplacing(NeoForgeVersions, FallbackNeoForge);
        }
        finally
        {
            IsLoadingNeoForge = false;
        }

        SelectedNeoForgeVersion = NeoForgeVersions.FirstOrDefault();
    }

    private static void FillReplacing(ObservableCollection<string> target, System.Collections.Generic.IEnumerable<string> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    partial void OnSelectedMinecraftVersionChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
            _ = LoadNeoForgeVersionsAsync(value);
    }

    private bool CanConfirm()
    {
        return !string.IsNullOrWhiteSpace(Name)
               && !string.IsNullOrEmpty(SelectedMinecraftVersion)
               && !string.IsNullOrEmpty(SelectedNeoForgeVersion);
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        var instance = _shell.CreateInstance(Name, SelectedMinecraftVersion!, SelectedNeoForgeVersion!);
        if (_pendingMods.Count > 0)
        {
            instance.Mods = _pendingMods.ToList();
            _shell.SaveInstance(instance);
        }

        _shell.BackToInstances();
    }

    [RelayCommand]
    private void Cancel()
    {
        _shell.BackToInstances();
    }
}
