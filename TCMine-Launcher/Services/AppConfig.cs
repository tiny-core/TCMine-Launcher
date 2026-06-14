using System.Linq;
using System.Reflection;

namespace TCMine_Launcher.Services;

/// <summary>
///     Configuração do launcher. O client ID do Azure é embutido no binário em
///     tempo de compilação a partir de <c>Client.props</c> (gitignored) — ver
///     <c>Client.props.example</c>. Não usa variáveis de ambiente nem ficheiros
///     de config externos.
/// </summary>
public static class AppConfig
{
    /// <summary>Client ID do Azure embutido no build (null se não configurado).</summary>
    public static string? MicrosoftClientId { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "MicrosoftClientId")?.Value is { Length: > 0 } value
            ? value.Trim()
            : null;
}