using Avalonia;
using Avalonia.Controls;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

/// <summary>
///     Linha de um servidor configurado de uma instância. O <c>DataContext</c> é o
///     <c>ServerEntry</c>; o comando de remover vem do
///     <see cref="InstanceModsPageViewModel" /> passado em <see cref="Owner" />.
/// </summary>
public partial class ServerEntryRow : UserControl
{
    /// <summary>ViewModel da página, fonte do comando de remover.</summary>
    public static readonly StyledProperty<InstanceModsPageViewModel?> OwnerProperty =
        AvaloniaProperty.Register<ServerEntryRow, InstanceModsPageViewModel?>(nameof(Owner));

    public ServerEntryRow()
    {
        InitializeComponent();
    }

    public InstanceModsPageViewModel? Owner
    {
        get => GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }
}