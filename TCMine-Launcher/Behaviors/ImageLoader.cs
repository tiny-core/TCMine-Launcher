using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.Behaviors;

/// <summary>
///     Propriedade anexada que carrega de forma assíncrona uma imagem a partir de um
///     URL e a atribui ao <see cref="Image.Source" />. Uso:
///     <c>&lt;Image bh:ImageLoader.SourceUrl="{Binding LogoUrl}" /&gt;</c>.
///     Falhas de rede são ignoradas (fica sem imagem). Camada de View.
/// </summary>
public static class ImageLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

    public static readonly AttachedProperty<string?> SourceUrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("SourceUrl", typeof(ImageLoader));

    static ImageLoader()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>((image, e) =>
            _ = LoadAsync(image, e.NewValue as string));
    }

    public static void SetSourceUrl(Image element, string? value)
    {
        element.SetValue(SourceUrlProperty, value);
    }

    public static string? GetSourceUrl(Image element)
    {
        return element.GetValue(SourceUrlProperty);
    }

    private static async Task LoadAsync(Image image, string? url)
    {
        image.Source = null;
        if (string.IsNullOrWhiteSpace(url)) return;

        // 1. Cache em memória.
        if (Cache.TryGetValue(url, out var cached))
        {
            image.Source = cached;
            return;
        }

        try
        {
            var diskPath = DiskPath(url);

            // 2. Cache em disco (persiste entre execuções).
            byte[] bytes;
            if (File.Exists(diskPath))
            {
                bytes = await File.ReadAllBytesAsync(diskPath);
            }
            else
            {
                bytes = await HttpClientProvider.Shared.GetByteArrayAsync(url);
                Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
                await File.WriteAllBytesAsync(diskPath, bytes);
            }

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            Cache[url] = bitmap;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Evita aplicar uma imagem antiga se o URL já mudou (reciclagem de item).
                if (GetSourceUrl(image) == url)
                    image.Source = bitmap;
            });
        }
        catch
        {
            // ignora imagens que falham a carregar
        }
    }

    private static string DiskPath(string url)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(LauncherPaths.ImageCacheDir, hash + ".img");
    }
}