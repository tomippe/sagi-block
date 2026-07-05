using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace SagiBlock.Helpers;

public static class TrayIconHelper
{
    private const string EmbeddedIconName = "SagiBlock.Assets.app-icon.png";
    private static readonly int[] TraySizes = [16, 20, 24, 32, 40, 48];

    public static Icon CreateTrayIcon()
    {
        using var source = LoadSourceBitmap();
        using var stream = new MemoryStream();
        WriteIco(stream, source);
        stream.Position = 0;
        using var loaded = new Icon(stream);
        return (Icon)loaded.Clone();
    }

    public static string EnsureLogoFilePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SagiBlock");
        Directory.CreateDirectory(dir);

        var path = Path.Combine(dir, "app-icon.png");
        if (File.Exists(path))
            return path;

        using var input = OpenEmbeddedIconStream();
        using var output = File.Create(path);
        input.CopyTo(output);
        return path;
    }

    private static Bitmap LoadSourceBitmap()
    {
        using var stream = OpenEmbeddedIconStream();
        return new Bitmap(stream);
    }

    private static Stream OpenEmbeddedIconStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetManifestResourceStream(EmbeddedIconName)
            ?? throw new InvalidOperationException($"Embedded icon not found: {EmbeddedIconName}");
    }

    private static void WriteIco(Stream stream, Bitmap source)
    {
        var pngs = TraySizes.Select(size => RenderPng(source, size)).ToList();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)pngs.Count);

        var offset = 6 + 16 * pngs.Count;
        for (var i = 0; i < TraySizes.Length; i++)
        {
            var size = TraySizes[i];
            var png = pngs[i];
            writer.Write((byte)size);
            writer.Write((byte)size);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)png.Length);
            writer.Write((uint)offset);
            offset += png.Length;
        }

        foreach (var png in pngs)
            writer.Write(png);
    }

    private static byte[] RenderPng(Bitmap source, int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, size, size));
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
