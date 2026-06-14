using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Fonte de manifestos de modpacks (resumos + detalhe). Abstrai a
///     <see cref="ManifestService" /> para o <see cref="ContentSyncService" /> poder ser
///     testado com um duplo, sem rede.
/// </summary>
public interface IManifestSource
{
    bool IsConfigured { get; }
    Task<List<ModpackManifest>> GetModpacksAsync(CancellationToken ct = default);
    Task<ModpackManifest?> GetManifestAsync(string id, CancellationToken ct = default);
}
