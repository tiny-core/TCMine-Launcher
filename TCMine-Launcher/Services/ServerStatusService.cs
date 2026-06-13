using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TCMine_Launcher.Services;

/// <summary>
///     Verifica se um servidor de Minecraft está acessível (ligação TCP à porta).
///     É uma verificação leve de online/offline — não faz o ping completo do protocolo.
/// </summary>
public class ServerStatusService
{
    public async Task<bool> IsOnlineAsync(string host, int port, int timeoutMs = 3000)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(host, port, cts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
