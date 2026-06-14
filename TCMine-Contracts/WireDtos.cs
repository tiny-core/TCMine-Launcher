namespace TCMine.Contracts;

// ─────────────────────────────────────────────────────────────────────────────
//  Contrato de wire entre o TCMine-Server e o launcher — FONTE ÚNICA do formato
//  JSON. Serializado em camelCase (default da web): ex. ModId -> "modId".
//  NÃO renomear campos sem alinhar ambos os lados.
// ─────────────────────────────────────────────────────────────────────────────

public record NewsDto(string Tag, string Title, string Date, string Summary);

public record ModpackSummaryDto(
    string Id, string Name, string Version, string Minecraft, string Neoforge,
    string Description, int ModCount, int ServerCount, DateTime UpdatedAt);

public record ModpackManifestDto(
    string Id, string Name, string Version, string Minecraft, string Neoforge,
    string Description, bool HasOverrides, int? RecommendedRamMb,
    IReadOnlyList<ModDto> Mods, IReadOnlyList<ServerDto> Servers);

public record ModDto(long ModId, long FileId, string Name, string FileName, string DownloadUrl, string Target, string? Version = null);

public record ServerDto(string Name, string Address, int Port);

public record ReleaseDto(string Version, string Notes, string Channel, DateTime PublishedAt);
