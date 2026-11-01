using System;
using System.Collections.Generic;
using System.Linq;

namespace PaperNotes;

public sealed class NoteManager
{
    public const string EveryCategory = "All";
    public const string NoCategory = "Uncategorized";
    public NoteStore Store { get; }
    public List<Note> Notes { get; }
    public Dictionary<Guid, NoteWindow> Windows { get; } = new();
    public event Action? Changed;
    public event Action? PreferencesChanged;
    public Action? IndexRequested { get; set; }
    public bool AllHidden { get; private set; }
    public Preferences Preferences { get; }
    public string CategoryFilter { get; private set; } = EveryCategory;
    public event Action? VisibilityChanged;
    public NoteManager(NoteStore store, Preferences? preferences = null)
    {
        Store = store; Notes = store.Load(); Preferences = preferences ?? new Preferences();
        Preferences.Normalize();
        CategoryFilter = NormalizeCategoryFilter(Preferences.ActiveCategory);
        Preferences.ActiveCategory = CategoryFilter;
        AllHidden = Preferences.AllHidden;
    }
    public Note Create(string content = "")
    {
        var categoryId = CategoryFilter != EveryCategory && CategoryFilter != NoCategory && CategoryOf(CategoryFilter) != null ? CategoryFilter : "";
        var note = new Note { Content = content, Color = Preferences.DefaultColor, IsAlwaysOnTop = Preferences.DefaultAlwaysOnTop, CategoryId = categoryId };
        WindowPlacement.CenterAtMouse(note);
        Store.Save(note); Notes.Add(note); Open(note); Changed?.Invoke(); return note;
    }
    public void Restore() { if (!AllHidden) foreach (var note in Notes.Where(n => !n.IsHidden).ToArray()) Open(note); }
    public NoteWindow Open(Note note)
    {
        if (AllHidden) ShowAll();
        if (!Windows.TryGetValue(note.Id, out var window))
        {
            window = new NoteWindow(note, Store); Windows[note.Id] = window;
            window.NewRequested = () => Create();
            window.IndexRequested = () => IndexRequested?.Invoke();
            window.DeleteRequested = () => Delete(note, true);
            window.Changed += _ => Changed?.Invoke();
            window.Closed += (_, _) => Windows.Remove(note.Id);
        }
        note.IsHidden = false; window.MarkDirty(); window.Flush();
        window.RestoreAndActivate(); return window;
    }
    public bool FlushAll() => Windows.Values.ToArray().Select(w => w.Flush()).ToArray().All(ok => ok);
    public Note[] Query(string query, string filter, string category = EveryCategory) => Notes
        .Where(n => (filter != "Pinned" || n.IsPinned) && (filter != "Hidden" || n.IsHidden || AllHidden)
            && MatchesCategory(n, category)
            && n.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(n => n.IsPinned).ThenByDescending(n => n.UpdatedAt).ToArray();
    public bool MatchesCategory(Note note, string category)
    {
        if (string.IsNullOrEmpty(category) || category == EveryCategory) return true;
        var known = CategoryOf(note.CategoryId) != null;
        if (category == NoCategory) return !known;
        return known && note.CategoryId == category;
    }
    public int CountIn(string category) => Notes.Count(n => MatchesCategory(n, category));
    public string CategoryName(string? categoryId) => CategoryOf(categoryId)?.Name ?? "";
    public void Decorate(Note note)
    {
        var category = CategoryOf(note.CategoryId);
        note.CategoryLabel = category?.Name ?? "";
        note.StripeOverride = category?.Color;
    }
    public string? CategoryNameError(string? name, string? exceptId)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) return Loc.T("Categories.NameRequired");
        if (trimmed.Length > 16) return Loc.T("Categories.NameTooLong");
        if (trimmed.Any(char.IsControl)) return Loc.T("Categories.NameHasNewline");
        if (Loc.IsReservedCategoryName(trimmed)) return Loc.T("Categories.ReservedName");
        if (Preferences.Categories.Any(c => c.Id != exceptId && string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
            return Loc.T("Categories.DuplicateName");
        return null;
    }
    public NoteCategory? AddCategory(string name, string? color)
    {
        if (CategoryNameError(name, null) != null) return null;
        if (!CategoryThemes.IsColor(color) || !CategoryThemes.Colors.Contains(color!)) color = CategoryThemes.Next(Preferences.Categories.Select(c => c.Color));
        var category = new NoteCategory { Name = name.Trim(), Color = color! };
        Preferences.Categories.Add(category);
        PreferencesChanged?.Invoke(); Changed?.Invoke(); return category;
    }
    public bool UpdateCategory(string id, string name, string color)
    {
        var category = CategoryOf(id);
        if (category == null || CategoryNameError(name, id) != null || !CategoryThemes.Colors.Contains(color)) return false;
        category.Name = name.Trim(); category.Color = color;
        PreferencesChanged?.Invoke(); Changed?.Invoke(); return true;
    }
    public bool DeleteCategory(string id)
    {
        var category = CategoryOf(id);
        if (category == null) return false;
        Preferences.Categories.Remove(category);
        int failed = 0;
        foreach (var note in Notes.Where(n => n.CategoryId == id).ToArray())
        {
            note.CategoryId = "";
            if (!TrySave(note)) failed++;
        }
        if (CategoryFilter == id) { CategoryFilter = EveryCategory; Preferences.ActiveCategory = EveryCategory; }
        PreferencesChanged?.Invoke(); Changed?.Invoke();
        if (failed > 0) System.Windows.MessageBox.Show(Loc.T("Categories.AssignmentSaveFailed"));
        return true;
    }
    public void AssignCategory(IEnumerable<Note> notes, string categoryId)
    {
        var id = categoryId == NoCategory ? "" : categoryId ?? "";
        if (id.Length > 0 && CategoryOf(id) == null) return;
        int failed = 0;
        foreach (var note in notes.Distinct())
        {
            var current = note.CategoryId ?? "";
            if (current == id) continue;
            note.CategoryId = id;
            if (TrySave(note)) continue;
            note.CategoryId = current;
            if (Windows.TryGetValue(note.Id, out var window)) window.MarkDirty();
            failed++;
        }
        Changed?.Invoke();
        if (failed > 0) System.Windows.MessageBox.Show(Loc.T("Categories.AssignmentSaveFailed"));
    }
    public void SetCategoryFilter(string filter)
    {
        filter = NormalizeCategoryFilter(filter);
        CategoryFilter = filter;
        if (Preferences.ActiveCategory == filter) return;
        Preferences.ActiveCategory = filter;
        PreferencesChanged?.Invoke();
    }
    public string NormalizeCategoryFilter(string? filter)
    {
        if (filter == NoCategory) return NoCategory;
        if (!string.IsNullOrEmpty(filter) && CategoryOf(filter) != null) return filter;
        return EveryCategory;
    }
    private NoteCategory? CategoryOf(string? categoryId) =>
        string.IsNullOrEmpty(categoryId) ? null : Preferences.Categories.FirstOrDefault(c => c.Id == categoryId);
    private bool TrySave(Note note)
    {
        try
        {
            if (Windows.TryGetValue(note.Id, out var window)) { window.MarkDirty(); return window.Flush(); }
            Store.Save(note); return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { return false; }
    }
    public bool CloseAll()
    {
        if (!FlushAll()) return false;
        foreach (var window in Windows.Values.ToArray()) window.Destroy();
        return true;
    }
    public void HideAll()
    {
        if (!FlushAll()) { System.Windows.MessageBox.Show(Loc.T("Notes.HideBlocked")); return; }
        AllHidden = true; Preferences.AllHidden = true; VisibilityChanged?.Invoke();
        foreach (var w in Windows.Values) w.Hide();
        Changed?.Invoke();
    }
    public void ShowAll()
    {
        AllHidden = false; Preferences.AllHidden = false; VisibilityChanged?.Invoke();
        foreach (var note in Notes.Where(n => !n.IsHidden).ToArray()) Open(note);
        Changed?.Invoke();
    }
    public void ToggleAll() { if (AllHidden) ShowAll(); else HideAll(); }
    public void RecoverOffscreenWindows()
    {
        foreach (var window in Windows.Values.Where(w => w.IsVisible && w.WindowState == System.Windows.WindowState.Normal).ToArray()) window.EnsureVisible();
    }
    public bool Delete(Note note, bool confirm)
    {
        if (confirm && System.Windows.MessageBox.Show(Loc.T("Note.DeleteConfirm"), Loc.T("Note.DeleteTitle"), System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No) != System.Windows.MessageBoxResult.Yes) return false;
        try
        {
            Store.Delete(note);
            if (Windows.TryGetValue(note.Id, out var window)) window.Destroy();
            Notes.Remove(note); Changed?.Invoke(); return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        { System.Windows.MessageBox.Show(Loc.T("Note.DeleteFailedPrefix") + "\n" + ex.Message); return false; }
    }
}
