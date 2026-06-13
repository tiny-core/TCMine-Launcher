namespace TCMine_Server.Services;

// DTOs que fixam o formato JSON consumido pelo launcher. Serializados em camelCase
// (default da web): ex. ModId -> "modId". NÃO alterar os nomes sem alinhar com o launcher.

public record NewsDto(string Tag, string Title, string Date, string Summary);

public record ModpackSummaryDto(
    string Id, string Name, string Version, string Minecraft, string Neoforge,
    string Description, int ModCount, int ServerCount);

public record ModpackManifestDto(
    string Id, string Name, string Version, string Minecraft, string Neoforge,
    string Description, bool HasOverrides, int? RecommendedRamMb,
    IReadOnlyList<ModDto> Mods, IReadOnlyList<ServerDto> Servers);

public record ModDto(long ModId, long FileId, string Name, string FileName, string DownloadUrl, string Target);

public record ServerDto(string Name, string Address, int Port);
