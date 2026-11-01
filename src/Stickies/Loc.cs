using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace PaperNotes;

/// <summary>UI localization from the embedded catalog. Default language is Japanese.</summary>
public static class Loc
{
    public const string Ja = "ja-JP";
    public const string Zh = "zh-CN";
    public const string En = "en-US";
    public static readonly string[] Supported = { Ja, Zh, En };

    static readonly object gate = new();
    static readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    static readonly Dictionary<string, string> singularEn = new(StringComparer.Ordinal);
    static string language = Ja;
    static bool loaded;
    static readonly List<string> loadErrors = new();

    public static string Language
    {
        get { lock (gate) return language; }
    }

    public static event Action? Changed;

    public static IReadOnlyList<string> LoadErrors
    {
        get { lock (gate) return loadErrors.ToArray(); }
    }

    /// <summary>Throws if the embedded catalog is empty or failed validation.</summary>
    public static void ThrowIfCatalogInvalid()
    {
        EnsureLoaded();
        lock (gate)
        {
            if (entries.Count == 0) throw new InvalidDataException("ui-text-translated.json produced zero entries.");
            if (loadErrors.Count > 0) throw new InvalidDataException(string.Join("\n", loadErrors));
        }
    }

    public static string BrandName => T("App.Name");

    public static void EnsureLoaded()
    {
        lock (gate)
        {
            if (loaded) return;
            loadErrors.Clear();
            entries.Clear();
            singularEn.Clear();
            try
            {
                using var stream = OpenCatalogStream()
                    ?? throw new FileNotFoundException("ui-text-translated.json embedded resource missing.");
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;
                void AddItems(JsonElement array)
                {
                    foreach (var item in array.EnumerateArray())
                    {
                        string key = item.GetProperty("key").GetString() ?? "";
                        if (key.Length == 0) continue;
                        if (entries.ContainsKey(key))
                        {
                            loadErrors.Add("duplicate key: " + key);
                            continue;
                        }
                        entries[key] = new Entry(
                            NormalizeText(item.TryGetProperty("ja", out var ja) ? ja.GetString() : null),
                            NormalizeText(item.TryGetProperty("zh", out var zh) ? zh.GetString() : null),
                            NormalizeText(item.TryGetProperty("en", out var en) ? en.GetString() : null));
                    }
                }
                if (root.TryGetProperty("items", out var items)) AddItems(items);
                if (root.TryGetProperty("additional_items", out var extra)) AddItems(extra);
                if (root.TryGetProperty("english_singular_overrides", out var singular))
                {
                    foreach (var prop in singular.EnumerateObject())
                        singularEn[prop.Name] = NormalizeText(prop.Value.GetString());
                }
                ValidateCatalogUnlocked();
            }
            catch (Exception ex)
            {
                loadErrors.Add(ex.Message);
            }
            loaded = true;
        }
    }

    static Stream? OpenCatalogStream()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
            if (name.EndsWith("ui-text-translated.json", StringComparison.OrdinalIgnoreCase))
                return asm.GetManifestResourceStream(name);
        string beside = Path.Combine(AppContext.BaseDirectory, "Resources", "ui-text-translated.json");
        if (File.Exists(beside)) return File.OpenRead(beside);
        return null;
    }

    static void ValidateCatalogUnlocked()
    {
        foreach (var (key, entry) in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Ja)) loadErrors.Add(key + ": empty ja");
            if (string.IsNullOrWhiteSpace(entry.Zh)) loadErrors.Add(key + ": empty zh");
            if (string.IsNullOrWhiteSpace(entry.En)) loadErrors.Add(key + ": empty en");
            var jaPh = Placeholders(entry.Ja);
            var zhPh = Placeholders(entry.Zh);
            var enPh = Placeholders(entry.En);
            if (!jaPh.SetEquals(zhPh) || !jaPh.SetEquals(enPh))
                loadErrors.Add(key + ": placeholder mismatch ja/zh/en");
            if (entry.Ja.Contains("\\n", StringComparison.Ordinal) || entry.Zh.Contains("\\n", StringComparison.Ordinal) || entry.En.Contains("\\n", StringComparison.Ordinal))
                loadErrors.Add(key + ": literal \\n");
            if (entry.Ja.Contains("&#x0a;", StringComparison.OrdinalIgnoreCase))
                loadErrors.Add(key + ": html newline entity");
        }
        foreach (var key in singularEn.Keys)
            if (!entries.ContainsKey(key)) loadErrors.Add("singular override missing entry: " + key);
    }

    public static string NormalizeLanguage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Ja;
        value = value.Trim();
        if (Supported.Contains(value, StringComparer.OrdinalIgnoreCase))
            return Supported.First(s => s.Equals(value, StringComparison.OrdinalIgnoreCase));
        return Ja;
    }

    public static bool SetLanguage(string? value, bool notify = true)
    {
        EnsureLoaded();
        string next = NormalizeLanguage(value);
        lock (gate)
        {
            if (language == next) return false;
            language = next;
        }
        if (notify) Changed?.Invoke();
        return true;
    }

    public static string T(string key)
    {
        EnsureLoaded();
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                loadErrors.Add("missing key at runtime: " + key);
                return FallbackMissing(key);
            }
            string text = Pick(entry, language);
            if (string.IsNullOrEmpty(text)) text = entry.Ja;
            if (string.IsNullOrEmpty(text)) return FallbackMissing(key);
            return text;
        }
    }

    public static string F(string key, params (string name, object? value)[] args)
    {
        string template = T(key);
        if (language == En && singularEn.TryGetValue(key, out var singularTemplate) && IsSingularCount(args))
            template = singularTemplate;
        foreach (var (name, value) in args)
            template = template.Replace("{" + name + "}", value?.ToString() ?? "", StringComparison.Ordinal);
        return template;
    }

    static bool IsSingularCount((string name, object? value)[] args)
    {
        foreach (var (name, value) in args)
        {
            if (name is "notes.Length" or "imported" or "count")
            {
                if (value is int i) return i == 1;
                if (value is long l) return l == 1;
                if (int.TryParse(value?.ToString(), out int n)) return n == 1;
            }
        }
        return false;
    }

    public static bool IsReservedCategoryName(string name)
    {
        EnsureLoaded();
        name = (name ?? "").Trim();
        if (name.Length == 0) return false;
        // Keep legacy Chinese reserved names so old data and mixed UI still validate.
        if (name is "全部" or "未分类") return true;
        foreach (var lang in Supported)
        {
            if (string.Equals(Raw(lang, "Categories.AllTab"), name, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(Raw(lang, "Categories.Uncategorized"), name, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static string Raw(string lang, string key)
    {
        EnsureLoaded();
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry)) return "";
            return Pick(entry, NormalizeLanguage(lang));
        }
    }

    static string Pick(Entry entry, string lang) => lang switch
    {
        Zh => entry.Zh,
        En => entry.En,
        _ => entry.Ja
    };

    static string FallbackMissing(string key) => ""; // missing keys are reported via LoadErrors; never show the key in UI

    static string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("&#x0a;", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\\n", "\n", StringComparison.Ordinal);
    }

    static HashSet<string> Placeholders(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        int i = 0;
        while (i < text.Length)
        {
            int open = text.IndexOf('{', i);
            if (open < 0) break;
            int close = text.IndexOf('}', open + 1);
            if (close < 0) break;
            set.Add(text.Substring(open, close - open + 1));
            i = close + 1;
        }
        return set;
    }

    readonly record struct Entry(string Ja, string Zh, string En);
}
