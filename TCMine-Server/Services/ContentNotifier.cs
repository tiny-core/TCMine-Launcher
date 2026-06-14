using System.Collections.Concurrent;
using System.Threading.Channels;

namespace TCMine_Server.Services;

/// <summary>
///     Sinaliza aos launchers ligados (via SSE em <c>/events</c>) que o conteúdo público
///     mudou — novidades ou modpacks editados/criados/eliminados no admin. Mantém uma
///     <see cref="Version" /> incremental e transmite-a a todos os subscritores quando
///     <see cref="Bump" /> é chamado. Singleton (estado em memória partilhado).
/// </summary>
public class ContentNotifier
{
    private readonly ConcurrentDictionary<Channel<long>, byte> _subscribers = new();
    private long _version = 1;

    /// <summary>Versão atual do conteúdo (muda a cada alteração no admin).</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Incrementa a versão e notifica todos os launchers ligados.</summary>
    public void Bump()
    {
        var v = Interlocked.Increment(ref _version);
        foreach (var sub in _subscribers.Keys)
            sub.Writer.TryWrite(v);
    }

    /// <summary>Regista um subscritor (um launcher ligado ao stream SSE).</summary>
    public ChannelReader<long> Subscribe(out Channel<long> channel)
    {
        // Capacidade 1 + DropOldest: só interessa a versão mais recente.
        channel = Channel.CreateBounded<long>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        _subscribers.TryAdd(channel, 0);
        return channel.Reader;
    }

    public void Unsubscribe(Channel<long> channel)
    {
        _subscribers.TryRemove(channel, out _);
        channel.Writer.TryComplete();
    }
}
