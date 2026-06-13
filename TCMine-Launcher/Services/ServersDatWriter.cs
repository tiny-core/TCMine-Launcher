using System.Collections.Generic;
using System.IO;
using System.Linq;
using fNbt;
using TCMine_Launcher.Models;

namespace TCMine_Launcher.Services;

/// <summary>
///     Escreve/atualiza o <c>servers.dat</c> (NBT) da instância para que os
///     servidores do modpack apareçam na lista multijogador do jogo. Faz merge:
///     não duplica servidores já presentes nem apaga os que o jogador adicionou.
/// </summary>
public static class ServersDatWriter
{
    public static void Ensure(string gameDir, IEnumerable<ServerEntry> servers)
    {
        var list = servers.ToList();
        if (list.Count == 0) return;

        Directory.CreateDirectory(gameDir);
        var path = Path.Combine(gameDir, "servers.dat");

        NbtCompound root;
        NbtList serverList;

        if (File.Exists(path))
        {
            var existing = new NbtFile();
            existing.LoadFromFile(path);
            root = existing.RootTag;
            var found = root.Get<NbtList>("servers");
            if (found is null)
            {
                found = new NbtList("servers", NbtTagType.Compound);
                root.Add(found);
            }
            serverList = found;
        }
        else
        {
            serverList = new NbtList("servers", NbtTagType.Compound);
            root = new NbtCompound("") { serverList };
        }

        var existingIps = serverList
            .OfType<NbtCompound>()
            .Select(c => c.Get<NbtString>("ip")?.Value)
            .Where(ip => ip is not null)
            .ToHashSet();

        foreach (var server in list)
        {
            if (existingIps.Contains(server.Ip)) continue;
            serverList.Add(new NbtCompound
            {
                new NbtString("name", server.Name),
                new NbtString("ip", server.Ip)
            });
        }

        new NbtFile(root).SaveToFile(path, NbtCompression.None);
    }
}
