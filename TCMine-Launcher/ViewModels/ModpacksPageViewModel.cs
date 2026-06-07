using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Página "Modpacks": catálogo de modpacks disponíveis para instalar.
///     Por agora os dados são estáticos (sem API/disco).
/// </summary>
public partial class ModpacksPageViewModel : ViewModelBase
{
    [ObservableProperty] private Modpack? _selectedModpack;

    public ModpacksPageViewModel()
    {
        Modpacks = new ObservableCollection<Modpack>
        {
            new()
            {
                Name = "TCMine Modpack",
                Author = "Você",
                Version = "1.0.0",
                Tagline = "OFICIAL",
                Description = "O pack custom do servidor TCMine.",
                IsInstalled = false
            },
            new()
            {
                Name = "TCMine Lite",
                Author = "Você",
                Version = "0.4.2",
                Tagline = "LEVE",
                MinecraftVersion = "1.20.1",
                NeoForgeVersion = "47.1.106",
                Description = "Versão reduzida para PCs mais fracos.",
                IsInstalled = false
            },
            new()
            {
                Name = "Vanilla+",
                Author = "Comunidade",
                Version = "2.1.0",
                Tagline = "QoL",
                Description = "Apenas melhorias de qualidade de vida.",
                IsInstalled = true
            }
        };
    }

    public ObservableCollection<Modpack> Modpacks { get; }
}
