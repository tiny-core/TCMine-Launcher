using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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
var dataDir = Path.GetDirectoryName(Path.GetFullPath(dbPath))!;

// ── Segredos persistentes (CF_API_KEY / ADMIN_PASSWORD) ──────────────────────
// O ZimaOS/compose corrompe valores com '$' (ex.: a API key do CurseForge, formato
// bcrypt) — faz escaping a cada reinício. Solução automática: na PRIMEIRA arranque
// guardamos o valor da env num ficheiro em <dados>/secrets/ (o ficheiro não passa pela
// interpolação do compose) e, a partir daí, lemos sempre do ficheiro, ignorando a env
// (já corrompida). Não é preciso criar nada à mão. Para MUDAR um segredo, apaga o
// respetivo ficheiro e arranca com a nova env. Override explícito: CF_API_KEY_FILE /
// ADMIN_PASSWORD_FILE apontam para um ficheiro próprio (lido tal como está).
var secretsDir = Path.Combine(dataDir, "secrets");
foreach (var name in new[] { "CF_API_KEY", "ADMIN_PASSWORD" })
{
    var explicitFile = builder.Configuration[$"{name}_FILE"];
    var path = string.IsNullOrWhiteSpace(explicitFile)
        ? Path.Combine(secretsDir, name.ToLowerInvariant())
        : explicitFile;

    if (File.Exists(path))
    {
        // Ficheiro manda: imune à corrupção da env entre reinícios.
        builder.Configuration[name] = File.ReadAllText(path).Trim();
    }
    else if (!string.IsNullOrWhiteSpace(builder.Configuration[name]))
    {
        // Primeira arranque com a env ainda limpa: persiste para os próximos reinícios.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, builder.Configuration[name]!.Trim());
    }
}

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddScoped<ContentService>();
builder.Services.AddScoped<CurseForgeService>();
builder.Services.AddScoped<PlayerConfigStore>();
builder.Services.AddSingleton<OverridesStore>();
builder.Services.AddSingleton<ContentNotifier>();
builder.Services.AddSingleton<MinecraftAuthService>();

// Persiste as chaves de Data Protection no volume (junto da BD) para que os cookies
// de admin e os tokens antiforgery sobrevivam a reinícios/atualizações do container.
var keysDir = Path.Combine(dataDir, "keys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("TCMine-Server");

// ── CurseForge proxy (HttpClient + cache) ────────────────────────────────────
builder.Services.AddHttpClient("curseforge", client =>
{
    client.BaseAddress = new Uri("https://api.curseforge.com");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddMemoryCache();

// ── Rate limiting (por IP) dos endpoints públicos ────────────────────────────
// Protege o proxy CurseForge e o PUT de configs do jogador contra abuso.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("public", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1), PermitLimit = 120, QueueLimit = 0
            }));

    options.AddPolicy("configs", ctx =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1), PermitLimit = 30, QueueLimit = 0
            }));
});

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

// Cria/atualiza o esquema da BD no arranque. O conteúdo é gerido em /admin.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

app.UseCors();
app.UseRateLimiter();
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
}).RequireRateLimiting("public");

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

// Notas (changelog) de uma release por versão — usadas no modal de update do launcher.
app.MapGet("/releases/{version}", async (string version, ContentService content, CancellationToken ct) =>
{
    var release = await content.GetReleaseAsync(version, ct);
    return release is null ? Results.NotFound() : Results.Json(release);
});

// Bundle de overrides do modpack (configs/resourcepacks/options) — zip.
app.MapGet("/modpacks/{id}/overrides", (string id, OverridesStore store) =>
{
    var file = store.GetFile(id);
    return file is null ? Results.NotFound() : Results.File(file, "application/zip");
});

// ── Configs do jogador (sync entre PCs) ──────────────────────────────────────
// Guardadas por (uuid, modpackId) como um zip. A LEITURA (GET) é aberta (settings de
// jogo, sem segredos); a ESCRITA (PUT) exige um access token Minecraft válido que
// pertença ao UUID (validado contra a Mojang, fail-open — ver MinecraftAuthService).
const long maxConfigBytes = 25 * 1024 * 1024; // 25 MB — limite defensivo do PUT.

// Aceita só slugs simples nas chaves (defesa, embora sejam só chaves de BD).
static bool IsValidKey(string s)
{
    return !string.IsNullOrWhiteSpace(s) && s.Length <= 80 &&
           s.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}

// Extrai o token de "Authorization: Bearer <token>".
static string? BearerToken(HttpContext ctx)
{
    var h = ctx.Request.Headers.Authorization.ToString();
    return h.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? h["Bearer ".Length..].Trim()
        : null;
}

app.MapGet("/players/{uuid}/configs/{modpackId}", async (
    string uuid, string modpackId, PlayerConfigStore store, CancellationToken ct) =>
{
    if (!IsValidKey(uuid) || !IsValidKey(modpackId)) return Results.BadRequest();

    var entry = await store.GetAsync(uuid, modpackId, ct);
    if (entry is null) return Results.NotFound();

    // O launcher usa o X-Updated-At para decidir se a versão do servidor é mais recente.
    return Results.File(entry.Data, "application/zip",
        lastModified: new DateTimeOffset(entry.UpdatedAt, TimeSpan.Zero));
});

app.MapPut("/players/{uuid}/configs/{modpackId}", async (
    string uuid, string modpackId, HttpContext ctx, PlayerConfigStore store,
    MinecraftAuthService auth, CancellationToken ct) =>
{
    if (!IsValidKey(uuid) || !IsValidKey(modpackId)) return Results.BadRequest();

    // Escrita exige um token Minecraft válido que pertença a este UUID.
    var token = BearerToken(ctx);
    if (token is null) return Results.Unauthorized();
    if (!await auth.AuthorizeAsync(token, uuid, ct)) return Results.StatusCode(403);

    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms, ct);
    if (ms.Length == 0 || ms.Length > maxConfigBytes) return Results.BadRequest();

    var updatedAt = await store.UpsertAsync(uuid, modpackId, ms.ToArray(), ct);
    return Results.Json(new { updatedAt });
}).RequireRateLimiting("configs");

// ── Stream de eventos (SSE) — avisa os launchers que o conteúdo mudou ─────────
// O launcher liga-se a /events e recarrega novidades/modpacks quando recebe uma
// versão nova. Leitura pública (sem auth), tal como /news e /modpacks.
app.MapGet("/events", async (HttpContext ctx, ContentNotifier notifier, CancellationToken ct) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers["X-Accel-Buffering"] = "no"; // desliga o buffering do nginx

    var reader = notifier.Subscribe(out var channel);
    try
    {
        // Evento inicial com a versão atual (o cliente fixa-a como baseline).
        await ctx.Response.WriteAsync($"data: {notifier.Version}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            // Espera por uma nova versão, com keep-alive periódico (comentário SSE)
            // para manter a ligação viva através de proxies/firewalls.
            long version;
            try
            {
                using var hb = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hb.CancelAfter(TimeSpan.FromSeconds(25));
                version = await reader.ReadAsync(hb.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                await ctx.Response.WriteAsync(": keep-alive\n\n", ct);
                await ctx.Response.Body.FlushAsync(ct);
                continue;
            }

            await ctx.Response.WriteAsync($"data: {version}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException)
    {
        // Cliente desligou — normal.
    }
    finally
    {
        notifier.Unsubscribe(channel);
    }
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