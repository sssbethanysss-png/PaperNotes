using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Rasterize assets/app-icon.svg (sticky note logo) to a multi-size Windows .ico.
/// Uses WPF only (no NuGet). Reads path/line data from the SVG when present.
/// </summary>
public static class Program
{
    static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    [STAThread]
    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string outIco = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "assets", "app-icon.ico");
        string also = args.Length > 2 ? Path.GetFullPath(args[2]) : Path.Combine(root, "src", "Stickies", "app.ico");
        string svgPath = args.Length > 3 ? Path.GetFullPath(args[3]) : Path.Combine(root, "assets", "app-icon.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(outIco)!);

        var art = IconArt.Load(svgPath);
        var master = art.Render(512);
        SavePng(master, Path.Combine(Path.GetDirectoryName(outIco)!, "app-icon-512.png"));
        SavePng(Resize(master, 256), Path.Combine(Path.GetDirectoryName(outIco)!, "app-icon-256.png"));

        var frames = Sizes.Select(s => EncodePng(Resize(master, s))).ToList();
        WriteIco(outIco, frames);
        Directory.CreateDirectory(Path.GetDirectoryName(also)!);
        File.Copy(outIco, also, true);
        Console.WriteLine($"OK svg={svgPath}");
        Console.WriteLine($"OK ico={outIco} sizes={string.Join(",", Sizes)}");
        Console.WriteLine($"OK copy={also}");
        return 0;
    }

    static BitmapSource Resize(BitmapSource source, int size)
    {
        double scale = size / (double)source.PixelWidth;
        var scaled = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            dc.DrawImage(scaled, new Rect(0, 0, size, size));
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    static void SavePng(BitmapSource bmp, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    static byte[] EncodePng(BitmapSource bmp)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    static void WriteIco(string path, List<byte[]> pngFrames)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)pngFrames.Count);
        int offset = 6 + 16 * pngFrames.Count;
        for (int i = 0; i < pngFrames.Count; i++)
        {
            int size = Sizes[i];
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)32);
            bw.Write(pngFrames[i].Length);
            bw.Write(offset);
            offset += pngFrames[i].Length;
        }
        foreach (var frame in pngFrames) bw.Write(frame);
    }
}

sealed class IconArt
{
    public string BodyPath { get; init; } = "";
    public string FoldPath { get; init; } = "";
    public string HighlightPath { get; init; } = "";
    public List<(Point A, Point B)> Lines { get; init; } = new();
    public double ViewBox { get; init; } = 512;

    public static IconArt Load(string svgPath)
    {
        if (!File.Exists(svgPath)) return Defaults();
        string svg = File.ReadAllText(svgPath);
        var paths = Regex.Matches(svg, "<path\\b[^>]*\\bd\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(m => Regex.Replace(m.Groups[1].Value, "\\s+", " ").Trim())
            .Where(d => d.Length > 0)
            .ToList();
        var lines = Regex.Matches(svg, "<line\\b[^>]*>", RegexOptions.IgnoreCase)
            .Select(m => m.Value)
            .Select(tag => (
                A: new Point(Attr(tag, "x1"), Attr(tag, "y1")),
                B: new Point(Attr(tag, "x2"), Attr(tag, "y2"))))
            .Where(l => l.A.X != 0 || l.A.Y != 0 || l.B.X != 0 || l.B.Y != 0)
            .ToList();
        var vb = Regex.Match(svg, "viewBox\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        double box = 512;
        if (vb.Success)
        {
            var parts = vb.Groups[1].Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 && double.TryParse(parts[2], out var w) && double.TryParse(parts[3], out var h))
                box = Math.Max(w, h);
        }
        if (paths.Count < 2) return Defaults();
        return new IconArt
        {
            BodyPath = paths[0],
            FoldPath = paths.Count > 1 ? paths[1] : Defaults().FoldPath,
            HighlightPath = paths.Count > 2 ? paths[2] : (paths.Count > 1 ? paths[1].Split(new[] { 'L', 'l' }, 2)[0] : Defaults().HighlightPath),
            Lines = lines.Count > 0 ? lines : Defaults().Lines,
            ViewBox = box,
        };
    }

    static double Attr(string tag, string name)
    {
        var m = Regex.Match(tag, name + "\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    static IconArt Defaults() => new()
    {
        BodyPath = "M 128,72 L 384,72 A 40,40 0 0,1 424,112 L 424,336 L 336,424 L 128,424 A 40,40 0 0,1 88,384 L 88,112 A 40,40 0 0,1 128,72 Z",
        FoldPath = "M 336,424 C 346,375 375,346 424,336 L 364,336 A 28,28 0 0,0 336,364 Z",
        HighlightPath = "M 336,424 C 346,375 375,346 424,336",
        Lines =
        {
            (new Point(148, 168), new Point(310, 168)),
            (new Point(148, 236), new Point(348, 236)),
            (new Point(148, 304), new Point(260, 304)),
        },
        ViewBox = 512,
    };

    public RenderTargetBitmap Render(int size)
    {
        double scale = size / ViewBox;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale, scale));
            var body = Geometry.Parse(BodyPath);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(46, 30, 27, 24)), null, Offset(body, 0, 16));
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(26, 30, 27, 24)), null, Offset(body, 0, 4));

            var bodyGrad = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0xFF, 0xF9, 0xA6), 0),
                    new GradientStop(Color.FromRgb(0xFF, 0xE8, 0x53), 0.6),
                    new GradientStop(Color.FromRgb(0xFF, 0xD8, 0x36), 1),
                },
                new Point(0, 0), new Point(1, 1));
            dc.DrawGeometry(bodyGrad, null, body);

            var linePen = new Pen(new SolidColorBrush(Color.FromArgb(191, 0x3D, 0x30, 0x21)), 22)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            foreach (var line in Lines)
                dc.DrawGeometry(null, linePen, new LineGeometry(line.A, line.B));

            var fold = Geometry.Parse(FoldPath);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(56, 0x3D, 0x2E, 0x00)), null, Offset(fold, -4, 5));
            var foldGrad = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0xFF, 0xD6, 0x33), 0),
                    new GradientStop(Color.FromRgb(0xE5, 0xA8, 0x00), 0.7),
                    new GradientStop(Color.FromRgb(0xC7, 0x8C, 0x00), 1),
                },
                new Point(0, 0), new Point(1, 1));
            dc.DrawGeometry(foldGrad, null, fold);

            var highlight = new Pen(
                new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(204, 255, 255, 255), 0),
                        new GradientStop(Color.FromArgb(51, 0xFF, 0xF5, 0x9D), 1),
                    },
                    new Point(0, 0), new Point(1, 1)),
                3.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            try { dc.DrawGeometry(null, highlight, Geometry.Parse(HighlightPath)); }
            catch (FormatException) { }

            dc.Pop();
        }

        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    static Geometry Offset(Geometry geometry, double dx, double dy)
    {
        var g = geometry.Clone();
        g.Transform = new TranslateTransform(dx, dy);
        return g;
    }
}
