using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperNotes;

public sealed class Note
{
    public const double MinimumHeight = 40;
    public const double CompactHeight = 96;
    public const double CollapsedWidth = 280;
    public const double CollapsedHeight = 42;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = "";
    // v2 additive fields. Content stays authoritative for search, TXT, and v1 notes.
    public int FormatVersion { get; set; } = 1;
    // JSON name kept for compatibility; value is Base64 XAML (legacy RTF still loadable).
    public string? RichTextRtfBase64 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public double X { get; set; } = 160;
    public double Y { get; set; } = 160;
    public double Width { get; set; } = 300;
    public double Height { get; set; } = 300;
    /// <summary>Size restored after expand; independent of Width/Height while collapsed.</summary>
    public double ExpandedWidth { get; set; } = 300;
    public double ExpandedHeight { get; set; } = 300;
    /// <summary>Collapsed strip mode (not Compact Mode).</summary>
    public bool IsCollapsed { get; set; }
    public string Color { get; set; } = "Yellow";
    /// <summary>Category id. Empty/missing on older notes means uncategorized.</summary>
    public string CategoryId { get; set; } = "";
    public bool IsAlwaysOnTop { get; set; }
    public bool IsPinned { get; set; }
    public bool IsHidden { get; set; }
    public bool IsTranslucent { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string Preview => string.IsNullOrWhiteSpace(Content) ? Loc.T("Note.BlankTitle") : Content.Replace('\r', ' ').Replace('\n', ' ');
    [System.Text.Json.Serialization.JsonIgnore] public string ColorHex => Palette.Colors.GetValueOrDefault(Color, "#FFF0B5");
    [System.Text.Json.Serialization.JsonIgnore] public string? StripeOverride { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string StripeHex => string.IsNullOrEmpty(StripeOverride) ? ColorHex : StripeOverride;
    [System.Text.Json.Serialization.JsonIgnore] public string CategoryLabel { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public bool HasCategory => CategoryLabel.Length > 0;
    [System.Text.Json.Serialization.JsonIgnore] public string Timestamp => UpdatedAt.ToLocalTime().ToString("MM-dd HH:mm");
    [System.Text.Json.Serialization.JsonIgnore]
    public string Title => Content.Split('\n').Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0) is string line
        ? (line.Length > 60 ? line[..60] + "…" : line) : Loc.T("Note.Untitled");
}

public static class Palette
{
    public static readonly Dictionary<string, string> Colors = new()
    {
        ["Yellow"] = "#FFF0B5", ["Pink"] = "#F9DDE3", ["Blue"] = "#DCECF9",
        ["Green"] = "#E0EFD9", ["Purple"] = "#E9DFF5", ["Gray"] = "#E8E8E5", ["White"] = "#FFFEFA"
    };
    public static string DisplayName(string colorKey) => colorKey switch
    {
        "Yellow" => Loc.T("Colors.PaleYellow"),
        "Pink" => Loc.T("Colors.CherryPink"),
        "Blue" => Loc.T("Colors.SkyBlue"),
        "Green" => Loc.T("Colors.PaleGreen"),
        "Purple" => Loc.T("Colors.Lavender"),
        "Gray" => Loc.T("Colors.MistGray"),
        "White" => Loc.T("Colors.Ivory"),
        _ => colorKey
    };
}

public sealed class NoteCategory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Color { get; set; } = CategoryThemes.Default;
}

public static class CategoryThemes
{
    public const string Default = "#7C9A62";
    public static readonly string[] Colors =
    {
        "#7C9A62", "#C4924A", "#6A94B8", "#C47A8A",
        "#8B7BB8", "#5E9E90", "#C47A52", "#8A9078"
    };
    public static bool IsColor(string? color) =>
        color != null && color.Length == 7 && color[0] == '#'
        && color.Skip(1).All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    public static string Next(IEnumerable<string> used)
    {
        var counts = new Dictionary<string, int>();
        foreach (var color in used)
        {
            if (string.IsNullOrEmpty(color)) continue;
            counts[color] = counts.GetValueOrDefault(color) + 1;
        }
        return Colors.OrderBy(color => counts.GetValueOrDefault(color)).ThenBy(color => Array.IndexOf(Colors, color)).First();
    }
}
