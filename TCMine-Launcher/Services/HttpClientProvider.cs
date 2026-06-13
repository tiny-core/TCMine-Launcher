using System.Net.Http;

namespace TCMine_Launcher.Services;

/// <summary>
///     <see cref="HttpClient" /> único e partilhado por toda a app — evita o
///     esgotamento de sockets que acontece ao criar um HttpClient por serviço.
/// </summary>
public static class HttpClientProvider
{
    public static HttpClient Shared { get; } = Create();

    private static HttpClient Create()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TCMine-Launcher/1.0");
        return client;
    }
}
