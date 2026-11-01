using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using PaperNotes;

static class CategoryTests
{
    public static void Run(string root)
    {
        var dir = Path.Combine(root, "categories");
        var store = new NoteStore(dir);
        var manager = new NoteManager(store);
        var research = manager.AddCategory("研究", null);
        Tests.Check(research != null && research!.Color == CategoryThemes.Colors[0], "first category receives the first theme color");
        Tests.Check(manager.AddCategory("研究", null) == null && manager.CategoryNameError("全部", null) != null, "duplicate and reserved category names are rejected");
        var essay = manager.AddCategory("随笔", CategoryThemes.Colors[1]);
        Tests.Check(essay != null && essay!.Color == CategoryThemes.Colors[1], "second category keeps its chosen color");
        var loose = new Note { Content = "loose note", Color = "Pink" };
        var focused = new Note { Content = "only research", Color = "Yellow", CategoryId = research!.Id };
        store.Save(loose); store.Save(focused);
        manager.Notes.Add(loose); manager.Notes.Add(focused);
        Tests.Check(manager.CountIn(NoteManager.EveryCategory) == 2 && manager.CountIn(NoteManager.NoCategory) == 1 && manager.CountIn(research!.Id) == 1, "category counts include uncategorized notes");
        Tests.Check(manager.Query("only", "All", research.Id).Length == 1 && manager.Query("loose", "All", research.Id).Length == 0, "search is limited to the active category");
        manager.Decorate(loose); manager.Decorate(focused);
        Tests.Check(loose.StripeHex == loose.ColorHex && loose.CategoryLabel == "", "uncategorized stripe keeps the note color");
        Tests.Check(focused.StripeHex == research.Color && focused.CategoryLabel == "研究", "categorized stripe uses the category color");
        manager.SetCategoryFilter(research.Id);
        var created = manager.Create("fresh research");
        Tests.Check(created.CategoryId == research.Id, "new notes adopt the active category");
        manager.SetCategoryFilter(NoteManager.NoCategory);
        var blank = manager.Create("fresh loose");
        Tests.Check(blank.CategoryId == "", "uncategorized tab creates an uncategorized note");
        manager.SetCategoryFilter(NoteManager.EveryCategory);
        var fromAll = manager.Create("from all");
        Tests.Check(fromAll.CategoryId == "", "all tab creates an uncategorized note");
        int saves = 0;
        manager.PreferencesChanged += () => saves++;
        manager.SetCategoryFilter(research.Id);
        manager.SetCategoryFilter(research.Id);
        Tests.Check(saves == 1 && manager.Preferences.ActiveCategory == research.Id, "active category is saved once");
        var settings = new SettingsStore(dir);
        settings.Save(manager.Preferences);
        var loaded = settings.Load();
        Tests.Check(loaded.Categories.Count == 2 && loaded.Categories[0].Name == "研究" && loaded.ActiveCategory == research.Id, "categories survive settings reload");
        var restarted = new NoteManager(new NoteStore(dir), loaded);
        Tests.Check(restarted.CategoryFilter == research.Id && restarted.Preferences.Categories.Count == 2, "saved category tab is restored");
        Tests.Check(new NoteStore(dir).Load().Single(n => n.Id == created.Id).CategoryId == research.Id, "category id survives note reload");
        manager.UpdateCategory(essay!.Id, "随笔集", CategoryThemes.Colors[2]);
        Tests.Check(manager.CategoryName(essay.Id) == "随笔集" && essay.Color == CategoryThemes.Colors[2], "rename keeps the category id and changes its color");
        manager.AssignCategory(new[] { loose }, research.Id);
        Tests.Check(loose.CategoryId == research.Id && new NoteStore(dir).Load().Single(n => n.Id == loose.Id).CategoryId == research.Id, "moving a note saves the category");
        manager.DeleteCategory(research.Id);
        Tests.Check(!manager.Preferences.Categories.Any(c => c.Id == research.Id) && loose.CategoryId == "" && manager.CategoryFilter == NoteManager.EveryCategory, "deleting the active category returns its notes and the tab to all");
        Tests.Check(new NoteStore(dir).Load().All(n => n.CategoryId != research.Id), "deleted category ids are cleared on disk");
        manager.CloseAll();

        var legacySettingsDir = Path.Combine(root, "category-settings");
        Directory.CreateDirectory(legacySettingsDir);
        File.WriteAllText(Path.Combine(legacySettingsDir, "settings.json"), "{\"DefaultColor\":\"Blue\",\"DefaultAlwaysOnTop\":false,\"AllHidden\":false}");
        var legacySettings = new SettingsStore(legacySettingsDir).Load();
        Tests.Check(legacySettings.DefaultColor == "Blue" && legacySettings.Categories.Count == 0 && legacySettings.ActiveCategory == "All", "old settings stay valid without categories");
        var legacyId = Guid.NewGuid();
        var legacyStore = new NoteStore(Path.Combine(root, "category-legacy-note"));
        File.WriteAllText(Path.Combine(legacyStore.DirectoryPath, legacyId + ".json"), "{\"Id\":\"" + legacyId + "\",\"Content\":\"old\",\"Color\":\"Pink\",\"CreatedAt\":\"2026-09-01T00:00:00Z\",\"UpdatedAt\":\"2026-09-01T00:00:00Z\"}");
        var legacyNote = legacyStore.Load().Single();
        Tests.Check(legacyNote.CategoryId == "" && legacyNote.Color == "Pink" && legacyNote.Content == "old", "notes without a category stay uncategorized");

        var uiStore = new NoteStore(Path.Combine(root, "category-ui"));
        var ui = new NoteManager(uiStore);
        var tab = ui.AddCategory("研究", CategoryThemes.Colors[0])!;
        ui.AddCategory("待办", CategoryThemes.Colors[3]);
        var inTab = new Note { Content = "only research", Color = "Yellow", CategoryId = tab.Id };
        var outside = new Note { Content = "loose note", Color = "Pink" };
        uiStore.Save(inTab); uiStore.Save(outside);
        ui.Notes.Add(inTab); ui.Notes.Add(outside);
        var index = new IndexWindow(ui);
        index.Show();
        Tests.Pump(150);
        var names = Walk(index).OfType<RadioButton>().Select(AutomationProperties.GetName).ToList();
        Tests.Check(names.Any(n => n.StartsWith(Loc.T("Categories.AllTab") + " (2)"))
            && names.Any(n => n.StartsWith(Loc.T("Categories.Uncategorized") + " (1)"))
            && names.Any(n => n.StartsWith("研究 (1)"))
            && names.Any(n => n.StartsWith("待办 (0)")), "index shows category tabs with counts");
        Tests.Check(Walk(index).OfType<Button>().Any(b => AutomationProperties.GetName(b) == Loc.T("Categories.NewTitle")), "index keeps an add-category button");
        var list = Walk(index).OfType<ListBox>().Single();
        Tests.Check(list.Items.Count == 2, "all tab lists every note");
        Walk(index).OfType<RadioButton>().Single(r => AutomationProperties.GetName(r).StartsWith("研究")).IsChecked = true;
        Tests.Pump(40);
        Tests.Check(ui.CategoryFilter == tab.Id && list.Items.Count == 1 && ((Note)list.Items[0]).StripeHex == tab.Color, "choosing a tab filters cards and binds the stripe");
        Tests.Check(Walk(index).OfType<TextBlock>().First(t => t.FontSize >= 25).Text == "研究", "heading follows the active category");
        Walk(index).OfType<TextBox>().Single().Text = "loose";
        Tests.Check(list.Items.Count == 0, "search does not leak notes from other categories");
        index.AllowClose = true; index.Close();
    }

    private static IEnumerable<DependencyObject> Walk(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var descendant in Walk(child)) yield return descendant;
        }
    }
}
