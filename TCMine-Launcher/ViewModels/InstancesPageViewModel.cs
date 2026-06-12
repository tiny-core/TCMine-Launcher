using System.Collections.ObjectModel;
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

    /// <summary>Chamado pelo shell quando a instância ativa muda.</summary>
    public void NotifyActiveChanged()
    {
        OnPropertyChanged(nameof(ActiveInstanceId));
    }

    [RelayCommand]
    private void OpenCreate()
    {
        _shell.ShowCreateInstance();
    }

    [RelayCommand]
    private void Select(MinecraftInstance instance)
    {
        _shell.SelectInstance(instance);
    }

    [RelayCommand]
    private void Play(MinecraftInstance instance)
    {
        _shell.SelectInstance(instance);
        _shell.NavigateToHome();
    }

    [RelayCommand]
    private void Delete(MinecraftInstance instance)
    {
        // Instâncias oficiais não podem ser eliminadas pela UI.
        if (!instance.IsOfficial)
            _shell.DeleteInstance(instance);
    }
}
