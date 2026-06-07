using CommunityToolkit.Mvvm.ComponentModel;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Classe base de todos os ViewModels do NeoLauncher.
///     Herda <see cref="ObservableObject" /> do CommunityToolkit que implementa
///     INotifyPropertyChanged — necessário para que os bindings do Avalonia
///     saibam quando uma propriedade mudou e atualizem a UI automaticamente.
///     Futuramente pode adicionar: navegação, logging, serviços partilhados.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}