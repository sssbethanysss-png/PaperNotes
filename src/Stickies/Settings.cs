using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace PaperNotes;

public sealed class Preferences
{
    public string DefaultColor { get; set; } = "Yellow";
    public bool DefaultAlwaysOnTop { get; set; }
    public bool AllHidden { get; set; }
    public List<NoteCategory> Categories { get; set; } = new();
    /// <summary>"All", "Uncategorized", or a category id.</summary>
    public string ActiveCategory { get; set; } = "All";
    /// <summary>Global hotkey chord for new note. Null/empty = unbound.</summary>
    public string? HotkeyCreate { get; set; }
    /// <summary>Global hotkey chord to hide/show all notes. Null/empty = unbound.</summary>
    public string? HotkeyToggleAll { get; set; }
    /// <summary>ja-JP / zh-CN / en-US. Missing on old installs → Japanese.</summary>
    public string? UiLanguage { get; set; }
    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(HotkeyCreate)) HotkeyCreate = null;
        else HotkeyCreate = HotkeyCreate.Trim();
        if (string.IsNullOrWhiteSpace(HotkeyToggleAll)) HotkeyToggleAll = null;
        else HotkeyToggleAll = HotkeyToggleAll.Trim();
        UiLanguage = Loc.NormalizeLanguage(UiLanguage);
        Categories ??= new List<NoteCategory>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clean = new List<NoteCategory>();
        foreach (var category in Categories)
        {
            if (category == null) continue;
            var name = (category.Name ?? "").Trim();
            if (name.Length == 0 || name.Length > 16 || Loc.IsReservedCategoryName(name) || name.Any(char.IsControl)) continue;
            if (!seenNames.Add(name)) continue;
            if (string.IsNullOrWhiteSpace(category.Id) || !seenIds.Add(category.Id))
            {
                category.Id = Guid.NewGuid().ToString("N");
                seenIds.Add(category.Id);
            }
            if (!CategoryThemes.IsColor(category.Color) || !CategoryThemes.Colors.Contains(category.Color)) category.Color = CategoryThemes.Default;
            category.Name = name;
            clean.Add(category);
        }
        Categories = clean;
        if (string.IsNullOrWhiteSpace(ActiveCategory)) ActiveCategory = "All";
    }
}

public sealed class SettingsStore
{
    private readonly string path;
    private readonly NoteStore writer;
    public bool RecoveredFromBackup { get; private set; }
    public SettingsStore(string directory) { writer = new NoteStore(directory); path = Path.Combine(directory, "settings.json"); }
    public Preferences Load()
    {
        if (!File.Exists(path)) return new Preferences();
        try { return Read(path); }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            RecoveredFromBackup = true;
            try
            {
                var recovered = Read(path + ".bak");
                File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks);
                File.Copy(path + ".bak", path, true); return recovered;
            }
            catch (Exception backup) when (backup is IOException or JsonException or InvalidDataException) { return new Preferences(); }
        }
    }
    private static Preferences Read(string file)
    {
        var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(file)) ?? throw new InvalidDataException();
        if (!Palette.Colors.ContainsKey(value.DefaultColor ?? "")) value.DefaultColor = "Yellow";
        value.Normalize();
        return value;
    }
    public void Save(Preferences preferences) => writer.WriteJson(path, preferences);
}

public static class Startup
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("PaperNotes") is string; } }
    public static string Command(string executable) => "\"" + executable + "\" --startup";
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("PaperNotes", Command(Environment.ProcessPath ?? throw new InvalidOperationException(Loc.T("App.PathUnavailable"))));
        else key.DeleteValue("PaperNotes", false);
    }
}
