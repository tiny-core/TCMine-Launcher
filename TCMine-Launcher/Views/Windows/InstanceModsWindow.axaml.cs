using System;
using Avalonia.Controls;
using TCMine_Launcher.ViewModels;

namespace TCMine_Launcher.Views;

public partial class InstanceModsWindow : Window
{
    public InstanceModsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is InstanceModsPageViewModel vm)
            vm.CloseRequested += OnCloseRequested;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is InstanceModsPageViewModel vm)
            vm.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested() => Close();
}
