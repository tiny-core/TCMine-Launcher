using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace TCMine_Launcher.Views;

/// <summary>
///     Botão "Atualizar" reutilizável: mostra ícone + texto e troca para um
///     spinner enquanto <see cref="IsLoading" /> é verdadeiro (ficando desativado).
/// </summary>
public partial class RefreshButton : UserControl
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<RefreshButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<RefreshButton, bool>(nameof(IsLoading));

    public RefreshButton()
    {
        InitializeComponent();
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }
}
