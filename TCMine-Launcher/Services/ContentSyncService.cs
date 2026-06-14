using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Sincroniza o estado das instâncias oficiais com o servidor — lógica de domínio
///     (sem UI). Usa o RESUMO (<c>/modpacks</c>) como fonte barata:
///     <list type="bullet">
///         <item>modpack ausente do resumo (despublicado) ⇒ <see cref="MinecraftInstance.IsDiscontinued" />;</item>
///         <item>só vai buscar o manifesto completo (e aplica metadados) quando o
///         <c>UpdatedAt</c> do modpack é mais recente do que o já sincronizado
///         (<see cref="MinecraftInstance.MetaSyncedAt" />) — sync incremental.</item>
///     </list>
///     Mods e versões continuam a exigir reinstalação explícita. Best-effort: se o
///     servidor estiver indisponível, nada muda. A gravação é delegada (testável).
/// </summary>
public class ContentSyncService
{
    private readonly IManifestSource _manifest;
    private readonly Action<MinecraftInstance> _persist;

    public ContentSyncService(IManifestSource manifest, Action<MinecraftInstance> persist)
    {
        _manifest = manifest;
        _persist = persist;
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

        List<ModpackManifest> summaries;
        try
        {
            summaries = await _manifest.GetModpacksAsync(ct);
        }
        catch
        {
            return false; // servidor offline / sem rede — não muda nada
        }

        var byId = summaries
            .Where(s => !string.IsNullOrEmpty(s.Id))
            .ToDictionary(s => s.Id);

        var changed = false;
        foreach (var instance in instances.ToList())
        {
            if (!instance.IsOfficial || string.IsNullOrEmpty(instance.ModpackId)) continue;

            var dirty = false;

            if (!byId.TryGetValue(instance.ModpackId, out var summary))
            {
                // Não está na lista de publicados ⇒ descontinuado (mantém o snapshot local).
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

                // Só busca o manifesto completo se o modpack mudou desde o último sync.
                if (instance.MetaSyncedAt is null || summary.UpdatedAt > instance.MetaSyncedAt)
                    dirty |= await ApplyChangedAsync(instance, summary, ct);
            }

            if (dirty)
            {
                _persist(instance);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    ///     Vai buscar o manifesto completo de uma instância cujo modpack mudou e aplica
    ///     metadados (se a versão for a mesma). Marca <see cref="MinecraftInstance.MetaSyncedAt" />.
    /// </summary>
    private async Task<bool> ApplyChangedAsync(MinecraftInstance instance, ModpackManifest summary,
        CancellationToken ct)
    {
        ModpackManifest? full;
        try
        {
            full = await _manifest.GetManifestAsync(instance.ModpackId!, ct);
        }
        catch
        {
            return false; // rede falhou a meio — tenta de novo no próximo sync
        }

        if (full is null) return false;

        // Só metadados quando a VERSÃO é a mesma; versão nova é um update a sério.
        if (full.Version == instance.ManifestVersion)
            ApplyMetadata(instance, full);

        instance.MetaSyncedAt = summary.UpdatedAt;
        return true; // MetaSyncedAt mudou ⇒ há sempre algo a persistir
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
