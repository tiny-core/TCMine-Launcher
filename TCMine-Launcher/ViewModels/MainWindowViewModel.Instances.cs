using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TCMine_Launcher.Models;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Parte do shell dedicada à <b>gestão de instâncias</b>: carregar do disco,
///     selecionar a ativa, criar/duplicar/eliminar, importar/exportar e instalar
///     a partir de manifestos oficiais. Orquestra o <see cref="InstanceService" />
///     e mantém a coleção partilhada com as páginas.
/// </summary>
public partial class MainWindowViewModel
{
    private readonly InstanceService _instances = new();

    /// <summary>Todas as instâncias instaladas (fonte única, partilhada com a página).</summary>
    public ObservableCollection<MinecraftInstance> Instances { get; } = new();

    /// <summary>Instância atualmente selecionada (a que a Home lança).</summary>
    [ObservableProperty] private MinecraftInstance? _activeInstance;

    /// <summary>Carrega as instâncias do disco e restaura a que estava selecionada.</summary>
    private void LoadInstances()
    {
        Instances.Clear();
        foreach (var instance in _instances.LoadAll())
            Instances.Add(instance);

        // Primeira execução: cria uma instância inicial (deletável). Os modpacks
        // oficiais vêm do servidor, na aba Modpacks.
        if (Instances.Count == 0)
            Instances.Add(CreateSeed());

        ActiveInstance =
            Instances.FirstOrDefault(i => i.Id == _game.SelectedInstanceId)
            ?? Instances.First();
    }

    /// <summary>Cria a instância inicial padrão (usada na 1.ª execução e após apagar a última).</summary>
    private MinecraftInstance CreateSeed() =>
        _instances.Create("Instância padrão", _game.MinecraftVersion, _game.NeoForgeVersion);

    /// <summary>Define a instância ativa (a que a Home lança) e persiste a escolha.</summary>
    public void SelectInstance(MinecraftInstance instance)
    {
        ActiveInstance = instance;
        _game.SelectedInstanceId = instance.Id;
        PersistSettings();
        Home.NotifyInstanceChanged();
        InstancesPage.NotifyActiveChanged();
    }

    /// <summary>Abre a pasta do jogo da instância no explorador de ficheiros.</summary>
    public void OpenInstanceFolder(MinecraftInstance instance)
    {
        var dir = LauncherPaths.InstanceGameDir(instance.Id);
        Directory.CreateDirectory(dir);
        try
        {
            Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
        }
        catch
        {
            // ignora falhas a abrir o explorador
        }
    }

    /// <summary>
    ///     Exporta uma instância para um zip. O trabalho de disco (compressão) corre
    ///     fora da thread da UI para não congelar a interface.
    /// </summary>
    public Task ExportInstanceAsync(MinecraftInstance instance, string zipPath)
    {
        return Task.Run(() => _instances.Export(instance, zipPath));
    }

    /// <summary>
    ///     Importa uma instância de um zip, adiciona-a e seleciona-a. A extração corre
    ///     fora da thread da UI; a atualização da lista (ObservableCollection) volta
    ///     automaticamente para a thread da UI após o <c>await</c>.
    /// </summary>
    public async Task<MinecraftInstance> ImportInstanceAsync(string zipPath)
    {
        var instance = await Task.Run(() => _instances.Import(zipPath));
        Instances.Insert(0, instance);
        SelectInstance(instance);
        return instance;
    }

    /// <summary>Abre a gestão de mods/instância numa janela separada.</summary>
    public void ShowInstanceMods(MinecraftInstance instance, bool isNew = false)
    {
        InstanceModsPage.Begin(instance, isNew);
        OpenModsWindowRequested?.Invoke(InstanceModsPage);
    }

    /// <summary>Indica se uma instância já tem ficheiros de jogo (define Jogar vs Instalar).</summary>
    public bool IsInstanceInstalled(MinecraftInstance instance)
    {
        return _instances.IsInstalled(instance);
    }

    /// <summary>Cria uma nova instância, persiste-a e seleciona-a.</summary>
    public MinecraftInstance CreateInstance(string name, string mcVersion, string neoForgeVersion)
    {
        var instance = _instances.Create(name, mcVersion, neoForgeVersion);
        Instances.Insert(0, instance);
        SelectInstance(instance);
        return instance;
    }

    /// <summary>Grava as alterações de uma instância no disco.</summary>
    public void SaveInstance(MinecraftInstance instance)
    {
        _instances.Save(instance);
    }

    /// <summary>
    ///     Recarrega as instâncias do disco (após edição numa janela) para os cartões
    ///     refletirem nome/versões atualizados. Preserva a instância ativa pelo Id.
    /// </summary>
    public void RefreshInstancesDisplay()
    {
        var activeId = ActiveInstance?.Id;
        Instances.Clear();
        foreach (var instance in _instances.LoadAll())
            Instances.Add(instance);

        ActiveInstance = Instances.FirstOrDefault(i => i.Id == activeId)
                         ?? Instances.FirstOrDefault();

        Home.NotifyInstanceChanged();
        InstancesPage.NotifyActiveChanged();
    }

    /// <summary>
    ///     Cria uma cópia editável (Manual) de uma instância como <b>rascunho</b> em
    ///     memória — NÃO grava em disco nem aparece na lista até ser concluída
    ///     (<see cref="CommitInstance" />). Útil para personalizar a partir de um
    ///     modpack oficial sem o alterar.
    /// </summary>
    public MinecraftInstance DuplicateInstance(MinecraftInstance source)
    {
        return new MinecraftInstance
        {
            Name = source.Name + " (cópia)",
            MinecraftVersion = source.MinecraftVersion,
            NeoForgeVersion = source.NeoForgeVersion,
            Source = InstanceSource.Manual,
            RamOverrideMb = source.RamOverrideMb,
            Mods = source.Mods
                .Select(m => new ModEntry
                {
                    ModId = m.ModId, FileId = m.FileId, Name = m.Name,
                    FileName = m.FileName, DownloadUrl = m.DownloadUrl
                }).ToList(),
            Servers = source.Servers
                .Select(s => new ServerEntry { Name = s.Name, Address = s.Address, Port = s.Port })
                .ToList()
        };
    }

    /// <summary>
    ///     Persiste uma instância editada na janela. Para um rascunho novo
    ///     (<paramref name="isNew" />), grava-o, adiciona-o à lista e seleciona-o.
    /// </summary>
    public void CommitInstance(MinecraftInstance instance, bool isNew)
    {
        _instances.Save(instance);

        if (isNew)
        {
            if (!Instances.Contains(instance))
                Instances.Insert(0, instance);
            SelectInstance(instance);
        }
    }

    /// <summary>Elimina uma instância (e a sua pasta). Garante que sobra sempre uma ativa.</summary>
    public void DeleteInstance(MinecraftInstance instance)
    {
        _instances.Delete(instance);
        Instances.Remove(instance);

        if (ActiveInstance == instance)
        {
            var fallback = Instances.FirstOrDefault();
            if (fallback is null)
            {
                fallback = CreateSeed();
                Instances.Add(fallback);
            }
            SelectInstance(fallback);
        }
    }

    /// <summary>
    ///     Instala (ou atualiza) uma instância a partir de um manifesto oficial:
    ///     copia versões, mods e servidores. Se já existir uma instância desse
    ///     modpack, atualiza-a em vez de duplicar. Seleciona-a no fim.
    /// </summary>
    public MinecraftInstance InstallFromManifest(ModpackManifest manifest)
    {
        var existing = Instances.FirstOrDefault(i => i.ModpackId == manifest.Id);
        if (existing is not null)
        {
            existing.Name = manifest.Name;
            existing.MinecraftVersion = manifest.Minecraft;
            existing.NeoForgeVersion = manifest.Neoforge;
            existing.ManifestVersion = manifest.Version;
            existing.Description = manifest.Description;
            existing.Mods = manifest.Mods;
            existing.Servers = manifest.Servers;
            existing.HasOverrides = manifest.HasOverrides;
            existing.OverridesVersion = null; // reaplica os overrides da nova versão
            _instances.Save(existing);
            SelectInstance(existing);
            return existing;
        }

        var instance = new MinecraftInstance
        {
            Name = manifest.Name,
            MinecraftVersion = manifest.Minecraft,
            NeoForgeVersion = manifest.Neoforge,
            Source = InstanceSource.OfficialManifest,
            ModpackId = manifest.Id,
            ManifestVersion = manifest.Version,
            Description = manifest.Description,
            HasOverrides = manifest.HasOverrides,
            RamOverrideMb = manifest.RecommendedRamMb, // RAM recomendada pelo modpack
            Mods = manifest.Mods,
            Servers = manifest.Servers,
            // Por defeito entra no primeiro servidor do modpack (se houver).
            AutoJoinServerName = manifest.Servers.FirstOrDefault()?.Name
        };
        _instances.Save(instance);
        Instances.Insert(0, instance);
        SelectInstance(instance);
        return instance;
    }
}
