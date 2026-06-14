using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TCMine_Server.Services;

/// <summary>
///     Acesso ao CurseForge a partir do servidor (usa a CF_API_KEY já configurada),
///     para a administração: pesquisar modpacks/mods e importar um modpack completo
///     (resolve os ficheiros — nome + URL — automaticamente, sem entrada manual).
/// </summary>
public class CurseForgeService
{
    private const int GameMinecraft = 432;
    private const int ClassMods = 6;
    private const int ClassModpacks = 4471;
    public const int LoaderNeoForge = 6;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly string? _apiKey;

    private readonly IHttpClientFactory _factory;

    public CurseForgeService(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _apiKey = config["CF_API_KEY"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    private HttpClient Api()
    {
        var client = _factory.CreateClient("curseforge");
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        return client;
    }

    /// <summary>Pesquisa modpacks de Minecraft pelo nome.</summary>
    public Task<List<CfMod>> SearchModpacksAsync(string query, CancellationToken ct = default)
    {
        return SearchAsync(ClassModpacks, query, null, ct);
    }

    /// <summary>Pesquisa mods de Minecraft (opcionalmente filtrados pela versão MC).</summary>
    public Task<List<CfMod>> SearchModsAsync(string query, string? gameVersion, CancellationToken ct = default)
    {
        return SearchAsync(ClassMods, query, gameVersion, ct);
    }

    private async Task<List<CfMod>> SearchAsync(int classId, string query, string? gameVersion, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query)) return new List<CfMod>();

        var url = $"/v1/mods/search?gameId={GameMinecraft}&classId={classId}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}&sortField=2&sortOrder=desc&pageSize=20";
        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";

        var resp = await Api().GetFromJsonAsync<CfListResponse>(url, Json, ct);
        return resp?.Data ?? new List<CfMod>();
    }

    /// <summary>Ficheiros de um mod (mais recentes primeiro), filtrados por versão/loader.</summary>
    public async Task<List<CfFile>> GetModFilesAsync(
        long modId, string? gameVersion, int? loaderType, CancellationToken ct = default)
    {
        if (!IsConfigured) return new List<CfFile>();

        var url = $"/v1/mods/{modId}/files?pageSize=50";
        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
        if (loaderType is { } lt)
            url += $"&modLoaderType={lt}";

        var resp = await Api().GetFromJsonAsync<CfFilesResponse>(url, Json, ct);
        return resp?.Data ?? new List<CfFile>();
    }

    /// <summary>
    ///     Importa um modpack do CurseForge: descarrega o ficheiro mais recente, lê o
    ///     <c>manifest.json</c> e resolve cada mod (nome + ficheiro + URL de download).
    /// </summary>
    public async Task<ImportedModpack?> ImportModpackAsync(long modpackId, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        // 1) Ficheiro mais recente do modpack (o arquivo .zip).
        var files = await GetModFilesAsync(modpackId, null, null, ct);
        var packFile = files.FirstOrDefault();
        if (packFile is null) return null;

        var packUrl = ResolveDownloadUrl(packFile);
        if (packUrl is null) return null;

        // 2) Descarrega e lê o manifest.json de dentro do zip.
        var web = _factory.CreateClient();
        await using var stream = await web.GetStreamAsync(packUrl, ct);
        using var zip = new ZipArchive(await BufferAsync(stream, ct), ZipArchiveMode.Read);
        var manifestEntry = zip.GetEntry("manifest.json");
        if (manifestEntry is null) return null;

        await using var ms = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<CfManifest>(ms, Json, ct);
        if (manifest is null) return null;

        // 3) Resolve os ficheiros dos mods em lote (nome + URL).
        var fileIds = manifest.Files.Select(f => f.FileID).ToList();
        var resolved = await GetFilesBulkAsync(fileIds, ct);
        var byId = resolved.ToDictionary(f => f.Id);

        // 4) Info dos mods em lote (nome + classe → mod/resourcepack/shaderpack).
        var modIds = manifest.Files.Select(f => f.ProjectID).Distinct().ToList();
        var info = await GetModsAsync(modIds, ct);

        var mods = new List<ImportedMod>();
        foreach (var entry in manifest.Files)
        {
            byId.TryGetValue(entry.FileID, out var file);
            info.TryGetValue(entry.ProjectID, out var mod);
            var url = file is null ? null : ResolveDownloadUrl(file);
            mods.Add(new ImportedMod(
                entry.ProjectID, entry.FileID,
                mod?.Name ?? file?.FileName ?? $"mod {entry.ProjectID}",
                file?.FileName ?? string.Empty,
                url ?? string.Empty,
                mod?.Target ?? "mod",
                file?.DisplayName));
        }

        var loader = manifest.Minecraft.ModLoaders.FirstOrDefault(l => l.Primary)
                     ?? manifest.Minecraft.ModLoaders.FirstOrDefault();

        // 5) Bundle de overrides (configs, resourcepacks, options.txt, …) do zip.
        var overrides = BuildOverridesZip(zip, manifest.Overrides ?? "overrides");

        return new ImportedModpack(
            manifest.Name ?? "Modpack importado",
            manifest.Version ?? "1.0.0",
            manifest.Minecraft.Version,
            ExtractNeoForgeVersion(loader?.Id),
            mods,
            overrides);
    }

    /// <summary>Reempacota a pasta de overrides do modpack (sem o prefixo) num zip próprio.</summary>
    private static byte[]? BuildOverridesZip(ZipArchive src, string folder)
    {
        var prefix = folder.TrimEnd('/') + "/";
        var entries = src.Entries
            .Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && !e.FullName.EndsWith("/"))
            .ToList();
        if (entries.Count == 0) return null;

        using var ms = new MemoryStream();
        using (var outZip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var e in entries)
            {
                var rel = e.FullName[prefix.Length..];
                if (string.IsNullOrEmpty(rel)) continue;
                var outEntry = outZip.CreateEntry(rel);
                using var inS = e.Open();
                using var outS = outEntry.Open();
                inS.CopyTo(outS);
            }
        }

        return ms.ToArray();
    }

    private async Task<List<CfFile>> GetFilesBulkAsync(IEnumerable<long> fileIds, CancellationToken ct)
    {
        var resp = await Api().PostAsJsonAsync("/v1/mods/files", new { fileIds }, ct);
        var parsed = await resp.Content.ReadFromJsonAsync<CfFilesResponse>(Json, ct);
        return parsed?.Data ?? new List<CfFile>();
    }

    private async Task<Dictionary<long, CfMod>> GetModsAsync(IEnumerable<long> modIds, CancellationToken ct)
    {
        var resp = await Api().PostAsJsonAsync("/v1/mods", new { modIds }, ct);
        var parsed = await resp.Content.ReadFromJsonAsync<CfListResponse>(Json, ct);
        return (parsed?.Data ?? new List<CfMod>()).ToDictionary(m => m.Id);
    }

    /// <summary>Mapeia a classe do CurseForge para a pasta de destino no cliente.</summary>
    public static string ClassToTarget(long classId)
    {
        return classId switch
        {
            12 => "resourcepack",
            6552 => "shaderpack",
            _ => "mod"
        };
    }

    /// <summary>downloadUrl da API, ou reconstrução do URL edge.forgecdn quando vem nulo.</summary>
    public static string? ResolveDownloadUrl(CfFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.DownloadUrl)) return file.DownloadUrl;
        if (string.IsNullOrWhiteSpace(file.FileName)) return null;
        return $"https://edge.forgecdn.net/files/{file.Id / 1000}/{file.Id % 1000}/{file.FileName}";
    }

    private static string ExtractNeoForgeVersion(string? loaderId)
    {
        // ex.: "neoforge-21.1.172" -> "21.1.172"
        return string.IsNullOrWhiteSpace(loaderId) ? string.Empty
            : loaderId.Contains('-') ? loaderId[(loaderId.LastIndexOf('-') + 1)..] : loaderId;
    }

    private static async Task<MemoryStream> BufferAsync(Stream source, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await source.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }
}

// ── DTOs da API CurseForge ───────────────────────────────────────────────────
public record CfListResponse([property: JsonPropertyName("data")] List<CfMod> Data);

public record CfFilesResponse([property: JsonPropertyName("data")] List<CfFile> Data);

public record CfMod(long Id, string Name, string? Summary, CfLogo? Logo, List<CfFile>? LatestFiles, long ClassId = 6)
{
    public string? LogoUrl => Logo?.Url;

    /// <summary>Destino do ficheiro no cliente (mod/resourcepack/shaderpack) pela classe CF.</summary>
    public string Target => CurseForgeService.ClassToTarget(ClassId);
}

public record CfLogo(string? Url);

public record CfFile(long Id, long ModId, string FileName, string? DownloadUrl, List<string>? GameVersions,
    string? DisplayName = null);

// ── Manifest de um modpack CurseForge (dentro do .zip) ───────────────────────
public record CfManifest(
    CfManifestMc Minecraft,
    string? Name,
    string? Version,
    List<CfManifestFile> Files,
    string? Overrides = "overrides");

public record CfManifestMc(string Version, List<CfManifestLoader> ModLoaders);

public record CfManifestLoader(string Id, bool Primary);

public record CfManifestFile(long ProjectID, long FileID, bool Required);

// ── Resultado da importação ──────────────────────────────────────────────────
public record ImportedModpack(
    string Name,
    string Version,
    string Minecraft,
    string Neoforge,
    List<ImportedMod> Mods,
    byte[]? Overrides);

public record ImportedMod(long ModId, long FileId, string Name, string FileName, string DownloadUrl, string Target, string? Version = null);