using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TCMine_Launcher.Views;

/// <summary>
///     Barra de título reutilizável para janelas sem chrome nativo
///     (<c>WindowDecorations="None"</c>). Mostra logótipo + título + botões
///     minimizar/fechar e trata o arrasto da janela. Resolve a janela-pai
///     sozinha (via <see cref="TopLevel" />), por isso não precisa de código
///     no code-behind de cada janela.
/// </summary>
public partial class TitleBar : UserControl
{
    /// <summary>Texto mostrado ao lado do logótipo.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TitleBar, string>(nameof(Title), "TCMine Launcher");

    /// <summary>Mostra o botão minimizar (desligar em diálogos/utilitários).</summary>
    public static readonly StyledProperty<bool> ShowMinimizeProperty =
        AvaloniaProperty.Register<TitleBar, bool>(nameof(ShowMinimize), true);

    public TitleBar()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool ShowMinimize
    {
        get => GetValue(ShowMinimizeProperty);
        set => SetValue(ShowMinimizeProperty, value);
    }

    private Window? Host => TopLevel.GetTopLevel(this) as Window;

    private void OnDrag(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            Host?.BeginMoveDrag(e);
    }

    private void OnMinimize(object? sender, RoutedEventArgs e)
    {
        if (Host is { } window) window.WindowState = WindowState.Minimized;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Host?.Close();
}
