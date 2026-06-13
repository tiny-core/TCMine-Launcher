using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Instâncias": lista as instalações isoladas, permite selecionar a
///     ativa, jogar e eliminar. A criação acontece numa página dedicada
///     (<see cref="CreateInstancePageViewModel" />). A coleção vem do shell.
/// </summary>
public partial class InstancesPageViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;

    public InstancesPageViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
    }

    /// <summary>Instâncias instaladas (partilhadas com o shell).</summary>
    public ObservableCollection<MinecraftInstance> Instances => _shell.Instances;

    /// <summary>Id da instância ativa — usado para realçar o cartão selecionado.</summary>
    public string? ActiveInstanceId => _shell.ActiveInstance?.Id;

    /// <summary>Ações ficam bloqueadas enquanto há um jogo aberto.</summary>
    public bool CanInteract => !_shell.IsGameRunning;

    /// <summary>Chamado pelo shell quando a instância ativa muda.</summary>
    public void NotifyActiveChanged()
    {
        OnPropertyChanged(nameof(ActiveInstanceId));
    }

    /// <summary>Chamado pelo shell quando o jogo abre/fecha.</summary>
    public void NotifyGameRunningChanged()
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    [RelayCommand]
    private void OpenCreate()
    {
        _shell.ShowCreateInstance();
    }

    [RelayCommand]
    private void ManageMods(MinecraftInstance instance)
    {
        _shell.ShowInstanceMods(instance);
    }

    [RelayCommand]
    private void Select(MinecraftInstance instance)
    {
        // Ativa a instância e volta à tela principal.
        _shell.SelectInstance(instance);
        _shell.NavigateToHome();
    }

    [RelayCommand]
    private void Duplicate(MinecraftInstance instance)
    {
        // Cria um rascunho da cópia e abre a janela; só grava ao concluir.
        var copy = _shell.DuplicateInstance(instance);
        _shell.ShowInstanceMods(copy, isNew: true);
    }

    [RelayCommand]
    private async Task Delete(MinecraftInstance instance)
    {
        // Instâncias oficiais não podem ser eliminadas pela UI.
        if (instance.IsOfficial) return;

        var confirmed = await _shell.ConfirmAsync(
            "Eliminar instância",
            $"Eliminar \"{instance.Name}\"? Isto remove a pasta (mods, mundos) e é irreversível.");

        if (confirmed)
            _shell.DeleteInstance(instance);
    }
}
