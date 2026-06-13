using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using TCMine_Server.Components;
using TCMine_Server.Data;
using TCMine_Server.Services;

// ─────────────────────────────────────────────────────────────────────────────
//  TCMine Server
//  • Proxy CurseForge (/v1/*) — injeta a x-api-key (env CF_API_KEY).
//  • Conteúdo (novidades + modpacks) servido a partir de SQLite (EF Core).
//  • Feed de update do launcher (Velopack) servido em /updates (ficheiros).
//  • Interface de administração em /admin (Blazor Server, senha ADMIN_PASSWORD).
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

// Config local (fora do git): CF_API_KEY, ADMIN_PASSWORD, etc. sem env vars.
// Re-adiciona as env vars a seguir para que continuem a ter prioridade em
// produção/Docker (o ficheiro local serve sobretudo para desenvolvimento).
builder.Configuration.AddJsonFile("appsettings.local.json", true, true);
builder.Configuration.AddEnvironmentVariables();

// ── Base de dados (SQLite via EF Core) ───────────────────────────────────────
var dbPath = builder.Configuration["DB_PATH"]
             ?? Path.Combine(builder.Environment.ContentRootPath, "tcmine.db");
builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<CurseForgeService>();
builder.Services.AddSingleton<OverridesStore>();

// ── CurseForge proxy (HttpClient + cache) ────────────────────────────────────
builder.Services.AddHttpClient("curseforge", client =>
{
    client.BaseAddress = new Uri("https://api.curseforge.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddMemoryCache();

// CORS aberto por defeito (proxy de leitura). Restringe via CF_ALLOWED_ORIGINS.
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

// ── Autenticação de admin (cookie + senha única) ─────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "tcmine_admin";
        options.LoginPath = "/admin/login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ── Blazor Server (Razor Components) ─────────────────────────────────────────
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

// Cria/atualiza o esquema e importa os dados legados (1.ª execução).
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
    await Seeder.SeedAsync(db, app.Environment.ContentRootPath,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("seed"));
}

app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Feed de updates do launcher (Velopack): ficheiros de release em /updates.
var updatesDir = app.Configuration["UPDATES_DIR"]
                 ?? Path.Combine(app.Environment.ContentRootPath, "updates");
Directory.CreateDirectory(updatesDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(updatesDir),
    RequestPath = "/updates",
    ServeUnknownFileTypes = true // .nupkg / RELEASES
});

// Porta de entrada do /admin: exige sessão (exceto a própria página/POST de login).
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    var isAdmin = path.StartsWithSegments("/admin");
    var isLogin = path.StartsWithSegments("/admin/login");
    if (isAdmin && !isLogin && ctx.User.Identity?.IsAuthenticated != true)
    {
        ctx.Response.Redirect("/admin/login");
        return;
    }

    await next();
});

var apiKey = app.Configuration["CF_API_KEY"];
var cacheMinutes = double.TryParse(app.Configuration["CF_CACHE_MINUTES"], out var m) ? m : 5;

// ── Health check / raiz ──────────────────────────────────────────────────────
app.MapGet("/", () => Results.Ok(new
{
    service = "TCMine Server",
    configured = !string.IsNullOrWhiteSpace(apiKey)
}));

// ── Proxy CurseForge (reencaminhamento 1:1 de /v1/*) ─────────────────────────
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

// ── Conteúdo público (BD) — mesmo contrato JSON de antes ─────────────────────
app.MapGet("/news", (ContentService content, CancellationToken ct) =>
    content.GetNewsAsync(ct));

app.MapGet("/modpacks", (ContentService content, CancellationToken ct) =>
    content.GetModpackSummariesAsync(ct));

app.MapGet("/modpacks/{id}", async (string id, ContentService content, CancellationToken ct) =>
{
    var manifest = await content.GetManifestAsync(id, ct);
    return manifest is null ? Results.NotFound() : Results.Json(manifest);
});

// Bundle de overrides do modpack (configs/resourcepacks/options) — zip.
app.MapGet("/modpacks/{id}/overrides", (string id, OverridesStore store) =>
{
    var file = store.GetFile(id);
    return file is null ? Results.NotFound() : Results.File(file, "application/zip");
});

// ── Autenticação de admin (form login/logout) ────────────────────────────────
app.MapPost("/auth/login", async (HttpContext ctx, IConfiguration cfg) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var password = form["password"].ToString();
    var expected = cfg["ADMIN_PASSWORD"];

    if (string.IsNullOrEmpty(expected) || password != expected)
        return Results.Redirect("/admin/login?error=1");

    var identity = new ClaimsIdentity(
        new[] { new Claim(ClaimTypes.Name, "admin") },
        CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
    return Results.Redirect("/admin");
}).DisableAntiforgery();

app.MapPost("/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/admin/login");
}).DisableAntiforgery();

// ── Interface de administração (Blazor) ──────────────────────────────────────
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();