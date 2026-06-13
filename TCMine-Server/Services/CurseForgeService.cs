using System.IO.Compression;
using System.Net.Http.Json;
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

    private readonly IHttpClientFactory _factory;
    private readonly string? _apiKey;

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
    public Task<List<CfMod>> SearchModpacksAsync(string query, CancellationToken ct = default) =>
        SearchAsync(ClassModpacks, query, null, ct);

    /// <summary>Pesquisa mods de Minecraft (opcionalmente filtrados pela versão MC).</summary>
    public Task<List<CfMod>> SearchModsAsync(string query, string? gameVersion, CancellationToken ct = default) =>
        SearchAsync(ClassMods, query, gameVersion, ct);

    private async Task<List<CfMod>> SearchAsync(int classId, string query, string? gameVersion, CancellationToken ct)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(query)) return new();

        var url = $"/v1/mods/search?gameId={GameMinecraft}&classId={classId}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}&sortField=2&sortOrder=desc&pageSize=20";
        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";

        var resp = await Api().GetFromJsonAsync<CfListResponse>(url, Json, ct);
        return resp?.Data ?? new();
    }

    /// <summary>Ficheiros de um mod (mais recentes primeiro), filtrados por versão/loader.</summary>
    public async Task<List<CfFile>> GetModFilesAsync(
        long modId, string? gameVersion, int? loaderType, CancellationToken ct = default)
    {
        if (!IsConfigured) return new();

        var url = $"/v1/mods/{modId}/files?pageSize=50";
        if (!string.IsNullOrWhiteSpace(gameVersion))
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
        if (loaderType is { } lt)
            url += $"&modLoaderType={lt}";

        var resp = await Api().GetFromJsonAsync<CfFilesResponse>(url, Json, ct);
        return resp?.Data ?? new();
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

        // 4) Nomes legíveis dos mods em lote.
        var modIds = manifest.Files.Select(f => f.ProjectID).Distinct().ToList();
        var names = await GetModNamesAsync(modIds, ct);

        var mods = new List<ImportedMod>();
        foreach (var entry in manifest.Files)
        {
            byId.TryGetValue(entry.FileID, out var file);
            var url = file is null ? null : ResolveDownloadUrl(file);
            mods.Add(new ImportedMod(
                entry.ProjectID, entry.FileID,
                names.GetValueOrDefault(entry.ProjectID) ?? file?.FileName ?? $"mod {entry.ProjectID}",
                file?.FileName ?? string.Empty,
                url ?? string.Empty));
        }

        var loader = manifest.Minecraft.ModLoaders.FirstOrDefault(l => l.Primary)
                     ?? manifest.Minecraft.ModLoaders.FirstOrDefault();

        return new ImportedModpack(
            manifest.Name ?? "Modpack importado",
            manifest.Version ?? "1.0.0",
            manifest.Minecraft.Version,
            ExtractNeoForgeVersion(loader?.Id),
            mods);
    }

    private async Task<List<CfFile>> GetFilesBulkAsync(IEnumerable<long> fileIds, CancellationToken ct)
    {
        var resp = await Api().PostAsJsonAsync("/v1/mods/files", new { fileIds }, ct);
        var parsed = await resp.Content.ReadFromJsonAsync<CfFilesResponse>(Json, ct);
        return parsed?.Data ?? new();
    }

    private async Task<Dictionary<long, string>> GetModNamesAsync(IEnumerable<long> modIds, CancellationToken ct)
    {
        var resp = await Api().PostAsJsonAsync("/v1/mods", new { modIds }, ct);
        var parsed = await resp.Content.ReadFromJsonAsync<CfListResponse>(Json, ct);
        return (parsed?.Data ?? new()).ToDictionary(m => m.Id, m => m.Name);
    }

    /// <summary>downloadUrl da API, ou reconstrução do URL edge.forgecdn quando vem nulo.</summary>
    public static string? ResolveDownloadUrl(CfFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.DownloadUrl)) return file.DownloadUrl;
        if (string.IsNullOrWhiteSpace(file.FileName)) return null;
        return $"https://edge.forgecdn.net/files/{file.Id / 1000}/{file.Id % 1000}/{file.FileName}";
    }

    private static string ExtractNeoForgeVersion(string? loaderId) =>
        // ex.: "neoforge-21.1.172" -> "21.1.172"
        string.IsNullOrWhiteSpace(loaderId) ? string.Empty
            : loaderId.Contains('-') ? loaderId[(loaderId.LastIndexOf('-') + 1)..] : loaderId;

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

public record CfMod(long Id, string Name, string? Summary, CfLogo? Logo, List<CfFile>? LatestFiles)
{
    public string? LogoUrl => Logo?.Url;
}

public record CfLogo(string? Url);
public record CfFile(long Id, long ModId, string FileName, string? DownloadUrl, List<string>? GameVersions);

// ── Manifest de um modpack CurseForge (dentro do .zip) ───────────────────────
public record CfManifest(CfManifestMc Minecraft, string? Name, string? Version, List<CfManifestFile> Files);
public record CfManifestMc(string Version, List<CfManifestLoader> ModLoaders);
public record CfManifestLoader(string Id, bool Primary);
public record CfManifestFile(long ProjectID, long FileID, bool Required);

// ── Resultado da importação ──────────────────────────────────────────────────
public record ImportedModpack(string Name, string Version, string Minecraft, string Neoforge, List<ImportedMod> Mods);
public record ImportedMod(long ModId, long FileId, string Name, string FileName, string DownloadUrl);
