using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TCMine_Launcher.Services;

/// <summary>
///     Escreve a saída do jogo (stdout/stderr) para um ficheiro de log e mantém as
///     últimas linhas em memória para mostrar no caso de crash. Thread-safe — os
///     eventos do processo chegam de threads do pool.
/// </summary>
public sealed class GameLogCapture : IDisposable
{
    private const int TailSize = 30;
    private readonly object _lock = new();
    private readonly Queue<string> _tail = new();
    private readonly StreamWriter _writer;

    public GameLogCapture(string logPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        _writer = new StreamWriter(logPath, append: false) { AutoFlush = true };
        LogPath = logPath;
    }

    public string LogPath { get; }

    public void Append(string? line)
    {
        if (line is null) return;
        lock (_lock)
        {
            _writer.WriteLine(line);
            _tail.Enqueue(line);
            while (_tail.Count > TailSize) _tail.Dequeue();
        }
    }

    /// <summary>Últimas linhas do log (para mostrar num crash).</summary>
    public string[] Tail()
    {
        lock (_lock)
        {
            return _tail.ToArray();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try { _writer.Dispose(); }
            catch { /* noop */ }
        }
    }
}
