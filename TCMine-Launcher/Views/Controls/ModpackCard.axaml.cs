using Avalonia;
using Avalonia.Controls;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

/// <summary>
///     Cartão de um modpack oficial no catálogo. O <c>DataContext</c> é o
///     <c>ModpackListItem</c>; o comando de instalar vem do
///     <see cref="ModpacksPageViewModel" /> passado em <see cref="Owner" />.
/// </summary>
public partial class ModpackCard : UserControl
{
    /// <summary>ViewModel da página, fonte do comando de instalar.</summary>
    public static readonly StyledProperty<ModpacksPageViewModel?> OwnerProperty =
        AvaloniaProperty.Register<ModpackCard, ModpacksPageViewModel?>(nameof(Owner));

    public ModpackCard()
    {
        InitializeComponent();
    }

    public ModpacksPageViewModel? Owner
    {
        get => GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }
}
