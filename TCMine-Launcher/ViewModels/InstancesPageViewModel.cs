using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
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

    /// <summary>True enquanto decorre uma operação de import/export (zip).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteract))]
    private bool _isBusy;

    /// <summary>Mensagem mostrada junto à barra de progresso durante import/export.</summary>
    [ObservableProperty] private string _busyStatus = string.Empty;

    /// <summary>Ações ficam bloqueadas enquanto há um jogo aberto ou uma operação em curso.</summary>
    public bool CanInteract => !_shell.IsGameRunning && !IsBusy;

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
    private void OpenFolder(MinecraftInstance instance)
    {
        _shell.OpenInstanceFolder(instance);
    }

    [RelayCommand]
    private async Task Export(MinecraftInstance instance)
    {
        var path = await _shell.SaveFileAsync($"{instance.Name}.zip");
        if (string.IsNullOrEmpty(path)) return;

        await RunBusyAsync($"A exportar \"{instance.Name}\"...",
            () => _shell.ExportInstanceAsync(instance, path));
    }

    [RelayCommand]
    private async Task Import()
    {
        var path = await _shell.OpenFileAsync();
        if (string.IsNullOrEmpty(path)) return;

        await RunBusyAsync("A importar instância...",
            () => _shell.ImportInstanceAsync(path));
    }

    /// <summary>
    ///     Executa uma operação de import/export: ativa o estado ocupado (desativa os
    ///     botões + mostra a barra de progresso), corre a tarefa e repõe o estado no
    ///     fim, mesmo em caso de erro.
    /// </summary>
    private async Task RunBusyAsync(string status, Func<Task> operation)
    {
        IsBusy = true;
        BusyStatus = status;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha na operação de instância: {Status}", status);
            // Erro na barra de estado global (persiste após esconder a faixa de progresso).
            _shell.SetBusy(false, 0, "Erro: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
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
