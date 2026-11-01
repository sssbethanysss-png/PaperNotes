using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace PaperNotes;

public static class AppIcon
{
    private static ImageSource? windowIcon;
    private static Drawing.Icon? trayIcon;

    public static ImageSource ForWindow
    {
        get
        {
            if (windowIcon != null) return windowIcon;
            // Prefer a PNG frame — WPF should not depend on GDI+ ICO decoding.
            byte[] png = ReadPngFrame(32) ?? ReadPngFrame(256) ?? throw new FileNotFoundException("app.ico has no PNG frames.");
            using var stream = new MemoryStream(png);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame.Freeze();
            windowIcon = frame;
            return windowIcon;
        }
    }

    public static Drawing.Icon ForTray
    {
        get
        {
            if (trayIcon != null) return trayIcon;
            // GDI+ Icon often fails on PNG-compressed ICO entries; build from a decoded PNG frame instead.
            byte[] png = ReadPngFrame(32) ?? ReadPngFrame(48) ?? ReadPngFrame(64)
                ?? throw new FileNotFoundException("app.ico has no tray-sized PNG frame.");
            using var stream = new MemoryStream(png);
            using var bitmap = new Drawing.Bitmap(stream);
            IntPtr handle = bitmap.GetHicon();
            try
            {
                using var temp = Drawing.Icon.FromHandle(handle);
                trayIcon = (Drawing.Icon)temp.Clone();
            }
            finally { DestroyIcon(handle); }
            return trayIcon;
        }
    }

    public static void Apply(Window window) => window.Icon = ForWindow;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static byte[]? ReadPngFrame(int preferredSize)
    {
        using var stream = OpenIcoStream();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ExtractPngFrame(ms.ToArray(), preferredSize);
    }

    private static Stream OpenIcoStream()
    {
        // Referenced library resources need ";component/" — entry exe (tests) is not the resource owner.
        var uris = new[]
        {
            new Uri("pack://application:,,,/PaperNotes;component/app.ico"),
            new Uri("pack://application:,,,/app.ico"),
        };
        foreach (var uri in uris)
        {
            try
            {
                var resource = Application.GetResourceStream(uri);
                if (resource?.Stream != null) return resource.Stream;
            }
            // Pack URI often fails under the test host; try the next candidate / loose file.
            catch (IOException) { }
            catch (System.Resources.MissingManifestResourceException) { }
            catch (UriFormatException) { }
        }
        string beside = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(beside)) return File.OpenRead(beside);
        string sourced = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "Stickies", "app.ico");
        sourced = Path.GetFullPath(sourced);
        if (File.Exists(sourced)) return File.OpenRead(sourced);
        throw new FileNotFoundException("Embedded app.ico was not found. Rebuild with ApplicationIcon / Resource.");
    }

    /// <summary>Parse Vista+ ICO (PNG payloads) written by tools/IconRaster.</summary>
    private static byte[]? ExtractPngFrame(byte[] ico, int preferredSize)
    {
        if (ico.Length < 6) return null;
        ushort count = BitConverter.ToUInt16(ico, 4);
        byte[]? fallback = null;
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + i * 16;
            if (entry + 16 > ico.Length) break;
            int width = ico[entry] == 0 ? 256 : ico[entry];
            int bytesInRes = BitConverter.ToInt32(ico, entry + 8);
            int imageOffset = BitConverter.ToInt32(ico, entry + 12);
            if (imageOffset < 0 || bytesInRes <= 0 || imageOffset + bytesInRes > ico.Length) continue;
            if (ico[imageOffset] != 0x89 || ico[imageOffset + 1] != (byte)'P') continue;
            var frame = new byte[bytesInRes];
            Buffer.BlockCopy(ico, imageOffset, frame, 0, bytesInRes);
            if (width == preferredSize) return frame;
            fallback ??= frame;
            if (width == 32) fallback = frame;
        }
        return fallback;
    }
}
