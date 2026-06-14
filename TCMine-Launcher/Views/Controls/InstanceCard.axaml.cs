using Avalonia;
using Avalonia.Controls;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

/// <summary>
///     Cartão de uma instância na lista. O <c>DataContext</c> é a própria
///     <c>MinecraftInstance</c>; os comandos (selecionar, gerir, eliminar…)
///     vêm do <see cref="InstancesPageViewModel" /> passado em <see cref="Owner" />.
/// </summary>
public partial class InstanceCard : UserControl
{
    /// <summary>ViewModel da página, fonte dos comandos do cartão.</summary>
    public static readonly StyledProperty<InstancesPageViewModel?> OwnerProperty =
        AvaloniaProperty.Register<InstanceCard, InstancesPageViewModel?>(nameof(Owner));

    public InstanceCard()
    {
        InitializeComponent();
    }

    public InstancesPageViewModel? Owner
    {
        get => GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }
}