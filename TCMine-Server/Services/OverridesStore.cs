namespace TCMine_Server.Services;

/// <summary>
///     Guarda/serve o bundle de overrides (configs, resourcepacks, options.txt, …)
///     de cada modpack como um zip em disco (<c>OVERRIDES_DIR</c>).
/// </summary>
public class OverridesStore
{
    private readonly string _dir;

    public OverridesStore(IConfiguration config, IWebHostEnvironment env)
    {
        _dir = config["OVERRIDES_DIR"] ?? Path.Combine(env.ContentRootPath, "overrides");
    }

    // Path.GetFileName protege contra path traversal (o id é um slug).
    private string PathFor(string id)
    {
        return Path.Combine(_dir, Path.GetFileName(id) + ".zip");
    }

    public bool Exists(string id)
    {
        return File.Exists(PathFor(id));
    }

    public string? GetFile(string id)
    {
        var path = PathFor(id);
        return File.Exists(path) ? path : null;
    }

    public async Task SaveAsync(string id, byte[] zip)
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllBytesAsync(PathFor(id), zip);
    }

    public void Delete(string id)
    {
        var path = PathFor(id);
        if (File.Exists(path)) File.Delete(path);
    }
}