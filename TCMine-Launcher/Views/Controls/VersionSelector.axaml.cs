using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace TCMine_Launcher.Views;

/// <summary>
///     Seletor de versão reutilizável (etiqueta + spinner + ComboBox). Desacoplado
///     do ViewModel: liga-se por propriedades (<see cref="ItemsSource" />,
///     <see cref="SelectedItem" /> two-way, <see cref="IsLoading" />), por isso
///     serve qualquer página sem interface partilhada.
/// </summary>
public partial class VersionSelector : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VersionSelector, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VersionSelector, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<string?> SelectedItemProperty =
        AvaloniaProperty.Register<VersionSelector, string?>(
            nameof(SelectedItem), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<VersionSelector, bool>(nameof(IsLoading));

    public VersionSelector()
    {
        InitializeComponent();
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }
}