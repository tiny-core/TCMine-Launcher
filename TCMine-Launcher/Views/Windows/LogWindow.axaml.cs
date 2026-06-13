using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

public partial class LogWindow : Window
{
    private INotifyCollectionChanged? _log;

    public LogWindow()
    {
        InitializeComponent();
        Opened += (_, _) => HookLog();
        Closed += (_, _) => UnhookLog();
    }

    // Acompanha o registo ao vivo: cada nova linha rola a vista para o fim.
    private void HookLog()
    {
        if (DataContext is not HomePageViewModel vm) return;
        _log = vm.LaunchLog;
        _log.CollectionChanged += OnLogChanged;
        LogScroll.ScrollToEnd();
    }

    private void UnhookLog()
    {
        if (_log is not null) _log.CollectionChanged -= OnLogChanged;
        _log = null;
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd());
}
