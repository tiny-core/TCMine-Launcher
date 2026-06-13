using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

// ─────────────────────────────────────────────────────────────────────────────
//  Proxy CurseForge do TCMine.
//  Reencaminha GET /v1/* para https://api.curseforge.com/v1/* injetando a
//  x-api-key (lida da env var CF_API_KEY). A key nunca chega ao cliente.
//  Ver docs/curseforge-proxy.md para o contrato consumido pelo launcher.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("curseforge", client =>
{
    client.BaseAddress = new Uri("https://api.curseforge.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddMemoryCache();

// CORS aberto por defeito (é um proxy de leitura). Restringe as origens se o
// expuseres publicamente — ver CF_ALLOWED_ORIGINS no README.
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration["CF_ALLOWED_ORIGINS"];
    options.AddDefaultPolicy(policy =>
    {
        if (string.IsNullOrWhiteSpace(origins))
            policy.AllowAnyOrigin();
        else
            policy.WithOrigins(origins.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();

var apiKey = app.Configuration["CF_API_KEY"];
var cacheMinutes = double.TryParse(app.Configuration["CF_CACHE_MINUTES"], out var m) ? m : 5;

// Health check / raiz.
app.MapGet("/", () => Results.Ok(new
{
    service = "TCMine CurseForge proxy",
    configured = !string.IsNullOrWhiteSpace(apiKey)
}));

// Reencaminhamento 1:1 das rotas /v1/* do CurseForge.
app.MapGet("/v1/{**path}", async (
    HttpContext ctx,
    string path,
    IHttpClientFactory factory,
    IMemoryCache cache,
    ILoggerFactory loggerFactory) =>
{
    var log = loggerFactory.CreateLogger("proxy");

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        log.LogError("CF_API_KEY não está configurada.");
        return Results.Problem("CF_API_KEY não configurada no servidor.", statusCode: 500);
    }

    var query = ctx.Request.QueryString.Value ?? string.Empty;
    var upstream = $"/v1/{path}{query}";

    // Cache leve das respostas GET para poupar quota da API.
    if (cache.TryGetValue(upstream, out string? cached) && cached is not null)
        return Results.Content(cached, "application/json");

    var client = factory.CreateClient("curseforge");
    using var request = new HttpRequestMessage(HttpMethod.Get, upstream);
    request.Headers.Add("x-api-key", apiKey);

    try
    {
        using var response = await client.SendAsync(request, ctx.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(ctx.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("CurseForge devolveu {Status} para {Upstream}", (int)response.StatusCode, upstream);
            return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
        }

        cache.Set(upstream, body, TimeSpan.FromMinutes(cacheMinutes));
        return Results.Content(body, "application/json");
    }
    catch (Exception ex)
    {
        log.LogError(ex, "Falha a contactar o CurseForge.");
        return Results.Problem("Falha a contactar o CurseForge.", statusCode: 502);
    }
});

// ── Modpacks oficiais (manifestos servidos a partir de ficheiros JSON) ───────
var modpacksDir = app.Configuration["MODPACKS_DIR"]
                  ?? Path.Combine(app.Environment.ContentRootPath, "modpacks");

static string? GetString(JsonElement e, string name) =>
    e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

// Lista (resumo: sem mods, só a contagem).
app.MapGet("/modpacks", () =>
{
    if (!Directory.Exists(modpacksDir))
        return Results.Json(Array.Empty<object>());

    var summaries = new List<object>();
    foreach (var file in Directory.EnumerateFiles(modpacksDir, "*.json"))
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;
            var modCount = root.TryGetProperty("mods", out var mods) && mods.ValueKind == JsonValueKind.Array
                ? mods.GetArrayLength()
                : 0;
            var serverCount = root.TryGetProperty("servers", out var srv) && srv.ValueKind == JsonValueKind.Array
                ? srv.GetArrayLength()
                : 0;

            summaries.Add(new
            {
                id = GetString(root, "id"),
                name = GetString(root, "name"),
                version = GetString(root, "version"),
                minecraft = GetString(root, "minecraft"),
                neoforge = GetString(root, "neoforge"),
                description = GetString(root, "description"),
                modCount,
                serverCount
            });
        }
        catch
        {
            // ficheiro inválido — ignora
        }
    }

    return Results.Json(summaries);
});

// Detalhe (manifesto completo com mods + servidores).
app.MapGet("/modpacks/{id}", (string id) =>
{
    var safe = Path.GetFileName(id); // evita path traversal
    var file = Path.Combine(modpacksDir, safe + ".json");
    return File.Exists(file)
        ? Results.Content(File.ReadAllText(file), "application/json")
        : Results.NotFound();
});

// ── Novidades do launcher (lidas de um ficheiro news.json) ───────────────────
var newsFile = app.Configuration["NEWS_FILE"]
               ?? Path.Combine(app.Environment.ContentRootPath, "news.json");

app.MapGet("/news", () =>
    File.Exists(newsFile)
        ? Results.Content(File.ReadAllText(newsFile), "application/json")
        : Results.Json(Array.Empty<object>()));

// ── Versão mais recente do launcher (auto-update) ────────────────────────────
var launcherFile = app.Configuration["LAUNCHER_FILE"]
                   ?? Path.Combine(app.Environment.ContentRootPath, "launcher.json");

app.MapGet("/launcher/latest", () =>
    File.Exists(launcherFile)
        ? Results.Content(File.ReadAllText(launcherFile), "application/json")
        : Results.NotFound());

app.Run();