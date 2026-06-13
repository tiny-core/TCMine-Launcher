using SkiaSharp;

// Gera o logótipo do TCMine (tile arredondado escuro + cubo isométrico laranja)
// em PNG (256px) e ICO multi-resolução. Uso: dotnet run -- <pasta-Assets>

var assetsDir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(assetsDir);

int[] sizes = { 16, 32, 48, 64, 128, 256 };
var pngs = new Dictionary<int, byte[]>();
foreach (var s in sizes)
    pngs[s] = Render(s);

File.WriteAllBytes(Path.Combine(assetsDir, "icon.png"), pngs[256]);
WriteIco(Path.Combine(assetsDir, "icon.ico"), pngs);

Console.WriteLine($"icon.png + icon.ico escritos em {Path.GetFullPath(assetsDir)}");
return;

static byte[] Render(int s)
{
    using var bmp = new SKBitmap(s, s, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);

    // Tile arredondado escuro com borda laranja
    var pad = s * 0.06f;
    var radius = s * 0.22f;
    var tile = new SKRect(pad, pad, s - pad, s - pad);

    using (var fill = new SKPaint { Color = new SKColor(0x1A, 0x0E, 0x06), IsAntialias = true })
        canvas.DrawRoundRect(tile, radius, radius, fill);

    using (var border = new SKPaint
           {
               Color = new SKColor(0xF9, 0x73, 0x16), IsAntialias = true,
               Style = SKPaintStyle.Stroke, StrokeWidth = MathF.Max(1f, s * 0.05f)
           })
    {
        var bw = border.StrokeWidth / 2f;
        var inner = new SKRect(tile.Left + bw, tile.Top + bw, tile.Right - bw, tile.Bottom - bw);
        canvas.DrawRoundRect(inner, radius - bw, radius - bw, border);
    }

    // Cubo isométrico de 3 faces
    float cx = s / 2f, cy = s * 0.52f, w = s * 0.26f;
    var top = new[] { P(cx, cy - w), P(cx + w, cy - w / 2), P(cx, cy), P(cx - w, cy - w / 2) };
    var left = new[] { P(cx - w, cy - w / 2), P(cx, cy), P(cx, cy + w), P(cx - w, cy + w / 2) };
    var right = new[] { P(cx + w, cy - w / 2), P(cx, cy), P(cx, cy + w), P(cx + w, cy + w / 2) };

    Face(canvas, top, new SKColor(0xFB, 0x92, 0x3C));
    Face(canvas, left, new SKColor(0xC2, 0x41, 0x0C));
    Face(canvas, right, new SKColor(0xF9, 0x73, 0x16));

    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    return data.ToArray();
}

static SKPoint P(float x, float y) => new(x, y);

static void Face(SKCanvas canvas, SKPoint[] pts, SKColor color)
{
    using var path = new SKPath();
    path.MoveTo(pts[0]);
    for (var i = 1; i < pts.Length; i++) path.LineTo(pts[i]);
    path.Close();
    using var paint = new SKPaint { Color = color, IsAntialias = true, Style = SKPaintStyle.Fill };
    canvas.DrawPath(path, paint);
}

// Escreve um .ico com cada tamanho como PNG embutido (suportado no Windows Vista+).
static void WriteIco(string path, Dictionary<int, byte[]> images)
{
    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);

    var entries = images.OrderBy(kv => kv.Key).ToList();
    w.Write((short)0);            // reserved
    w.Write((short)1);            // type = icon
    w.Write((short)entries.Count);

    var offset = 6 + entries.Count * 16;
    foreach (var (size, bytes) in entries)
    {
        w.Write((byte)(size >= 256 ? 0 : size)); // width  (0 = 256)
        w.Write((byte)(size >= 256 ? 0 : size)); // height (0 = 256)
        w.Write((byte)0);         // palette
        w.Write((byte)0);         // reserved
        w.Write((short)1);        // color planes
        w.Write((short)32);       // bits per pixel
        w.Write(bytes.Length);    // size of image data
        w.Write(offset);          // offset of image data
        offset += bytes.Length;
    }

    foreach (var (_, bytes) in entries)
        w.Write(bytes);
}
