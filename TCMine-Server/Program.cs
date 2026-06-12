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

app.Run();