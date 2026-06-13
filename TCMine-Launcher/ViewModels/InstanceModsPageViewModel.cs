using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página de gestão de mods de uma instância já existente. Reutiliza o
///     <see cref="ModSelectionViewModel" />; cada alteração é persistida de imediato
///     (os ficheiros são descarregados no próximo Instalar/Jogar).
/// </summary>
public partial class InstanceModsPageViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private MinecraftInstance? _instance;

    [ObservableProperty] private ModSelectionViewModel? _modSelection;
    [ObservableProperty] private string _instanceName = string.Empty;

    public InstanceModsPageViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
    }

    /// <summary>Prepara a página para uma instância concreta.</summary>
    public void Begin(MinecraftInstance instance)
    {
        _instance = instance;
        InstanceName = instance.Name;

        var selected = new ObservableCollection<ModEntry>(instance.Mods);
        ModSelection = new ModSelectionViewModel(
            _shell.CurseForge,
            selected,
            () => instance.MinecraftVersion,
            () =>
            {
                instance.Mods = selected.ToList();
                _shell.SaveInstance(instance);
            });
    }

    [RelayCommand]
    private void Back()
    {
        _shell.BackToInstances();
    }
}
