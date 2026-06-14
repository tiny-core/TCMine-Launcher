using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Definições": conta, memória JVM, caminho do Java e info.
/// </summary>
public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly GameProfile _game;
    private readonly PlayerProfile _player;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _javaPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RamDisplay))]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private double _ramMb;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _serverUrl;

    /// <summary>Estado da última ação "Guardar definições" (mensagem na UI).</summary>
    [ObservableProperty] private string? _settingsStatus;

    [ObservableProperty] private bool _isSyncing;

    public SettingsPageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        _ramMb = game.AllocatedRamMb;
        _javaPath = game.JavaPath ?? string.Empty;
        _serverUrl = game.ServerUrl ?? string.Empty;
    }

    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;
    public string? PlayerHeadUrl => _player.HeadUrl;

    public string RamDisplay => $"{(int)RamMb} MB";

    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
        OnPropertyChanged(nameof(PlayerHeadUrl));
    }

    // Nada é gravado em disco enquanto o utilizador edita — só ao clicar em "Guardar
    // definições" (ver SaveSettings). Os campos do VM só atualizam a UI.

    /// <summary>Há alterações por gravar (campos diferem do que está em <c>_game</c>).</summary>
    public bool IsDirty =>
        (int)RamMb != _game.AllocatedRamMb
        || (JavaPath ?? string.Empty) != (_game.JavaPath ?? string.Empty)
        || (ServerUrl ?? string.Empty).Trim() != (_game.ServerUrl ?? string.Empty);

    /// <summary>
    ///     Grava as definições (RAM, Java, URL) em disco e aplica o que delas depende:
    ///     reconecta ao stream de eventos e re-sincroniza novidades, modpacks e metadados
    ///     das instâncias. Única escrita em disco das definições.
    /// </summary>
    [RelayCommand]
    private async Task SaveSettings()
    {
        if (IsSyncing) return;

        _game.AllocatedRamMb = (int)RamMb;
        _game.JavaPath = string.IsNullOrWhiteSpace(JavaPath) ? null : JavaPath.Trim();
        _game.ServerUrl = string.IsNullOrWhiteSpace(ServerUrl) ? null : ServerUrl.Trim();
        _shell.PersistSettings();
        OnPropertyChanged(nameof(IsDirty));

        IsSyncing = true;
        SettingsStatus = "A guardar e atualizar...";
        try
        {
            await _shell.ReconnectAndSyncAsync();
            SettingsStatus = "Definições guardadas.";
        }
        catch
        {
            SettingsStatus = "Guardado, mas não foi possível contactar o servidor.";
        }
        finally
        {
            IsSyncing = false;
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _shell.LogoutCommand.Execute(null);
    }
}