using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace TCMine_Server.Services;

/// <summary>
///     Valida o access token do Minecraft (MSession) contra a API de perfil da Mojang
///     e confirma que pertence ao UUID indicado. Usado para autenticar a escrita das
///     configs do jogador (PUT). <b>Fail-open</b>: se a Mojang estiver inacessível,
///     autoriza (são settings de jogo, sem segredos) — nunca parte o sync por uma
///     indisponibilidade externa. Só NEGA quando o token é confirmadamente inválido ou
///     o UUID não corresponde. Resultados são cacheados em memória (~10 min).
/// </summary>
public class MinecraftAuthService
{
    private const string ProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MinecraftAuthService> _log;

    public MinecraftAuthService(IHttpClientFactory http, IMemoryCache cache, ILogger<MinecraftAuthService> log)
    {
        _http = http;
        _cache = cache;
        _log = log;
    }

    public async Task<bool> AuthorizeAsync(string token, string expectedUuid, CancellationToken ct)
    {
        var want = Normalize(expectedUuid);
        if (string.IsNullOrEmpty(token) || want is null) return false;

        var key = "mcauth:" + token;
        if (_cache.TryGetValue(key, out string? cachedUuid))
            return cachedUuid is not null && cachedUuid == want;

        try
        {
            var client = _http.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, ProfileUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var resp = await client.SendAsync(req, ct);

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _cache.Set(key, (string?)null, TimeSpan.FromMinutes(1)); // inválido confirmado
                return false;
            }

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Mojang devolveu {Status} ao validar token — fail-open", (int)resp.StatusCode);
                return true; // indisponível → não bloqueia o sync
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var id = doc.RootElement.TryGetProperty("id", out var v) ? Normalize(v.GetString()) : null;
            _cache.Set(key, id, TimeSpan.FromMinutes(10));
            return id is not null && id == want;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha a validar token na Mojang — fail-open");
            return true; // erro de rede → não bloqueia o sync
        }
    }

    /// <summary>UUID em minúsculas e sem hífens (formato consistente para comparação).</summary>
    private static string? Normalize(string? uuid) =>
        string.IsNullOrWhiteSpace(uuid) ? null : uuid.Replace("-", "").Trim().ToLowerInvariant();
}
