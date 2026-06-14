namespace TCMine.Core;

/// <summary>Resultado de uma mesclagem de listas de mods.</summary>
/// <typeparam name="T">Tipo do mod (ModEntry no cliente, ModEntryEntity no servidor).</typeparam>
public sealed record MergeResult<T>(List<T> Items, int Added, int Updated);

/// <summary>
///     Mescla (em vez de substituir) duas listas de mods por uma chave (o id do mod no
///     CurseForge). Lógica PURA, partilhada por servidor e cliente:
///     <list type="bullet">
///         <item>mod novo (chave inexistente) ⇒ adicionado;</item>
///         <item>mod já presente ⇒ atualizado (substituído pelo recebido);</item>
///         <item>mods atuais que não vêm na lista recebida ⇒ mantidos.</item>
///     </list>
/// </summary>
public static class ModSetMerge
{
    public static MergeResult<T> Merge<T>(
        IEnumerable<T> current, IEnumerable<T> incoming, Func<T, long> key)
    {
        // Preserva a ordem: primeiro os atuais (na sua ordem), depois os novos.
        var ordered = new List<T>();
        var index = new Dictionary<long, int>();

        foreach (var item in current)
        {
            index[key(item)] = ordered.Count;
            ordered.Add(item);
        }

        var added = 0;
        var updated = 0;
        foreach (var item in incoming)
        {
            var k = key(item);
            if (index.TryGetValue(k, out var pos))
            {
                ordered[pos] = item; // substitui (atualiza) mantendo a posição
                updated++;
            }
            else
            {
                index[k] = ordered.Count;
                ordered.Add(item);
                added++;
            }
        }

        return new MergeResult<T>(ordered, added, updated);
    }
}
