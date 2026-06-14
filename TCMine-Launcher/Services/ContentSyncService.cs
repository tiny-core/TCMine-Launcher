using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Sincroniza o estado das instâncias oficiais com o servidor — lógica de domínio
///     (sem UI). Para cada instância oficial:
///     <list type="bullet">
///         <item>marca/limpa <see cref="MinecraftInstance.IsDiscontinued" /> conforme o
///         modpack ainda está publicado (manifesto 404 ⇒ descontinuado);</item>
///         <item>Quando a versão é a mesma, aplica metadados baratos (nome, descrição,
///         servidores) que possam ter mudado sem subir a versão.</item>
///     </list>
///     Mods e versões continuam a exigir reinstalação explícita (envolvem downloads).
///     Best-effort: erros de rede deixam a instância inalterada.
/// </summary>
public class ContentSyncService
{
    private readonly InstanceService _instances;
    private readonly ManifestService _manifest;

    public ContentSyncService(ManifestService manifest, InstanceService instances)
    {
        _manifest = manifest;
        _instances = instances;
    }

    /// <summary>
    ///     Sincroniza todas as instâncias oficiais. Devolve <c>true</c> se alguma mudou
    ///     (o chamador deve então refrescar a UI). Faz o snapshot da coleção antes de
    ///     qualquer await, por isso deve ser invocada na thread da UI.
    /// </summary>
    public async Task<bool> SyncOfficialAsync(IEnumerable<MinecraftInstance> instances,
        CancellationToken ct = default)
    {
        if (!_manifest.IsConfigured) return false;

        var changed = false;
        foreach (var instance in instances.ToList())
        {
            if (!instance.IsOfficial || string.IsNullOrEmpty(instance.ModpackId)) continue;

            ModpackManifest? manifest;
            try
            {
                manifest = await _manifest.GetManifestAsync(instance.ModpackId, ct);
            }
            catch
            {
                continue; // servidor offline / sem rede — ignora esta instância
            }

            var dirty = false;

            if (manifest is null)
            {
                // Modpack despublicado/removido — marca como descontinuado (sem mexer no
                // snapshot: mods/servidores/mundos do jogador ficam intactos).
                if (!instance.IsDiscontinued)
                {
                    instance.IsDiscontinued = true;
                    dirty = true;
                }
            }
            else
            {
                if (instance.IsDiscontinued)
                {
                    instance.IsDiscontinued = false;
                    dirty = true;
                }

                // Só metadados quando a VERSÃO é a mesma; versão nova é update a sério.
                if (manifest.Version == instance.ManifestVersion)
                    dirty |= ApplyMetadata(instance, manifest);
            }

            if (dirty)
            {
                _instances.Save(instance);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>Aplica nome/descrição/servidores do manifesto à instância (em memória).</summary>
    private static bool ApplyMetadata(MinecraftInstance instance, ModpackManifest manifest)
    {
        var before = (instance.Name, instance.Description, ServersSignature(instance.Servers));

        instance.Name = manifest.Name;
        instance.Description = manifest.Description;
        instance.Servers = manifest.Servers;

        // Entrada automática apontava para um servidor que já não existe? Reaponta/limpa.
        if (instance.AutoJoinServerName is not null &&
            instance.Servers.All(s => s.Name != instance.AutoJoinServerName))
            instance.AutoJoinServerName = instance.Servers.FirstOrDefault()?.Name;

        var after = (instance.Name, instance.Description, ServersSignature(instance.Servers));
        return before != after;
    }

    /// <summary>Assinatura estável da lista de servidores (para detetar alterações).</summary>
    private static string ServersSignature(IEnumerable<ServerEntry> servers) =>
        string.Join("|", servers.Select(s => $"{s.Name}:{s.Address}:{s.Port}"));
}
