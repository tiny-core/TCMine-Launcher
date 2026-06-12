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

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RamDisplay))]
    private double _ramMb;

    [ObservableProperty] private string _javaPath;

    public SettingsPageViewModel(PlayerProfile player, GameProfile game, MainWindowViewModel shell)
    {
        _player = player;
        _game = game;
        _shell = shell;

        _ramMb = game.AllocatedRamMb;
        _javaPath = game.JavaPath ?? string.Empty;
    }

    public string PlayerName => _player.Name;
    public string AvatarInitials => _player.ComputeInitials();
    public string AccountLabel => _player.AccountLabel;

    public string RamDisplay => $"{(int)RamMb} MB";

    public void NotifyPlayerChanged()
    {
        OnPropertyChanged(nameof(PlayerName));
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AccountLabel));
    }

    partial void OnRamMbChanged(double value)
    {
        _game.AllocatedRamMb = (int)value;
        _shell.PersistSettings();
    }

    partial void OnJavaPathChanged(string value)
    {
        _game.JavaPath = string.IsNullOrWhiteSpace(value) ? null : value;
        _shell.PersistSettings();
    }

    [RelayCommand]
    private void Logout()
    {
        _shell.LogoutCommand.Execute(null);
    }
}
