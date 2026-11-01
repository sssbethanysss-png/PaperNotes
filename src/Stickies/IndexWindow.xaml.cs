using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PaperNotes;

public partial class IndexWindow : Window
{
    private readonly NoteManager manager;
    private bool buildingTabs;
    private string tabStamp = "";
    public Action? SettingsRequested { get; set; }
    public bool AllowClose { get; set; }
    public IndexWindow(NoteManager manager)
    {
        this.manager = manager; InitializeComponent();
        TitleBar.Attach(this, Loc.BrandName, showMaximize: true);
        AppIcon.Apply(this);
        ApplyLanguage();
        manager.Changed += Refresh;
        Loc.Changed += OnLanguageChanged;
        NotesList.ContextMenu = new ContextMenu();
        NotesList.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (ItemsControl.ContainerFromElement(NotesList, e.OriginalSource as DependencyObject) is ListBoxItem item && !item.IsSelected)
            { NotesList.SelectedItems.Clear(); item.IsSelected = true; }
        };
        NotesList.ContextMenuOpening += (_, e) =>
        {
            if (NotesList.SelectedItem is not Note note) { e.Handled = true; return; }
            var selected = NotesList.SelectedItems.Cast<Note>().ToArray();
            var menu = NotesList.ContextMenu; menu.Items.Clear();
            var open = new MenuItem { Header = Loc.T("Notes.Open"), IsEnabled = selected.Length == 1 }; open.Click += (_, _) => manager.Open(note); menu.Items.Add(open);
            menu.Items.Add(BuildCategoryMenu(selected));
            menu.Items.Add(new Separator());
            var delete = new MenuItem { Header = Loc.T("Note.DeleteMenu"), IsEnabled = selected.Length == 1 }; delete.Click += (_, _) => manager.Delete(note, true); menu.Items.Add(delete);
        };
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; Hide(); } };
        Closed += (_, _) =>
        {
            manager.Changed -= Refresh;
            Loc.Changed -= OnLanguageChanged;
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control) { manager.Create(); e.Handled = true; }
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { SearchBox.Focus(); e.Handled = true; } };
        Refresh();
    }
    void OnLanguageChanged()
    {
        tabStamp = "";
        ApplyLanguage();
        Refresh();
    }
    void ApplyLanguage()
    {
        Title = Loc.T("Notes.WindowTitle");
        TitleBar.RefreshBrand(this);
        if (NewNoteButton != null) NewNoteButton.Content = Loc.T("Notes.NewButton");
        if (SearchLabel != null) SearchLabel.Text = Loc.T("Notes.Search");
        if (SettingsButton != null) SettingsButton.ToolTip = Loc.T("Settings.Title");
        if (ImportButton != null) ImportButton.Content = Loc.T("Import.Text");
        if (ExportButton != null) ExportButton.Content = Loc.T("Export.TextButton");
        if (AddCategoryButton != null)
        {
            AddCategoryButton.ToolTip = Loc.T("Categories.NewTitle");
            AutomationProperties.SetName(AddCategoryButton, Loc.T("Categories.NewTitle"));
        }
    }
    public void Reveal() { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
    public void Refresh()
    {
        RefreshTabs();
        RefreshList();
    }
    private void RefreshTabs()
    {
        if (CategoryBar == null) return;
        var stamp = StructureStamp();
        if (stamp != tabStamp)
        {
            var offset = CategoryScroll.HorizontalOffset;
            buildingTabs = true;
            CategoryBar.Children.Clear();
            CategoryBar.Children.Add(CreateTab(NoteManager.EveryCategory, Loc.T("Categories.AllTab"), manager.CountIn(NoteManager.EveryCategory), null));
            CategoryBar.Children.Add(CreateTab(NoteManager.NoCategory, Loc.T("Categories.Uncategorized"), manager.CountIn(NoteManager.NoCategory), null));
            foreach (var category in manager.Preferences.Categories)
                CategoryBar.Children.Add(CreateTab(category.Id, category.Name, manager.CountIn(category.Id), category.Color));
            buildingTabs = false;
            tabStamp = stamp;
            Dispatcher.BeginInvoke(new Action(() => CategoryScroll.ScrollToHorizontalOffset(offset)));
        }
        ApplyActiveTab();
    }
    private string StructureStamp()
    {
        var text = Loc.Language + "|All:" + manager.CountIn(NoteManager.EveryCategory) + "|None:" + manager.CountIn(NoteManager.NoCategory);
        foreach (var category in manager.Preferences.Categories)
            text += "|" + category.Id + ":" + category.Name + ":" + category.Color + ":" + manager.CountIn(category.Id);
        return text;
    }
    private RadioButton CreateTab(string id, string label, int count, string? color)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        if (color != null)
            content.Children.Add(new Border
            {
                Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Ui.Brush(color),
                Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            });
        content.Children.Add(new TextBlock { Text = $"{label} ({count})", VerticalAlignment = VerticalAlignment.Center });
        var button = new RadioButton
        {
            Content = content, Tag = id, GroupName = "NoteCategories", ToolTip = label,
            Style = (Style)FindResource("CategoryTab"), FocusVisualStyle = null
        };
        AutomationProperties.SetName(button, $"{label} ({count})");
        button.Checked += TabChecked;
        if (color != null)
        {
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = Loc.T("Categories.EditMenu") }; edit.Click += (_, _) => EditCategory(id); menu.Items.Add(edit);
            var remove = new MenuItem { Header = Loc.T("Categories.DeleteMenu") }; remove.Click += (_, _) => RemoveCategory(id); menu.Items.Add(remove);
            button.ContextMenu = menu;
        }
        return button;
    }
    private void ApplyActiveTab()
    {
        buildingTabs = true;
        foreach (var child in CategoryBar.Children)
            if (child is RadioButton radio) radio.IsChecked = (string?)radio.Tag == manager.CategoryFilter;
        buildingTabs = false;
    }
    private void TabChecked(object sender, RoutedEventArgs e)
    {
        if (buildingTabs) return;
        if (sender is not RadioButton { Tag: string id } || id == manager.CategoryFilter) return;
        manager.SetCategoryFilter(id);
        RefreshList();
    }
    private void RefreshList()
    {
        if (NotesList == null || SearchBox == null) return;
        var selected = NotesList.SelectedItems.Cast<Note>().Select(n => n.Id).ToHashSet();
        var notes = manager.Query(SearchBox.Text, "All", manager.CategoryFilter);
        foreach (var note in notes) manager.Decorate(note);
        NotesList.ItemsSource = notes;
        foreach (var note in notes.Where(n => selected.Contains(n.Id))) NotesList.SelectedItems.Add(note);
        CountLabel.Text = SearchBox.Text.Length > 0
            ? Loc.F("Notes.SearchCount", ("notes.Length", notes.Length))
            : Loc.F("Notes.Count", ("notes.Length", notes.Length));
        Heading.Text = manager.CategoryFilter switch
        {
            NoteManager.NoCategory => Loc.T("Categories.Uncategorized"),
            NoteManager.EveryCategory => Loc.T("Notes.AllTitle"),
            _ => manager.CategoryName(manager.CategoryFilter) is { Length: > 0 } name ? name : Loc.T("Notes.AllTitle")
        };
        EmptyLabel.Visibility = notes.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (SearchBox.Text.Length > 0) EmptyLabel.Text = Loc.T("Notes.NoSearchResults");
        else if (manager.CategoryFilter == NoteManager.NoCategory) EmptyLabel.Text = Loc.T("Notes.UncategorizedEmpty");
        else if (manager.CategoryFilter != NoteManager.EveryCategory)
        {
            var name = manager.CategoryName(manager.CategoryFilter);
            EmptyLabel.Text = string.IsNullOrEmpty(name)
                ? Loc.T("Notes.EmptyRuntime")
                : Loc.F("Notes.CategoryEmpty", ("name", name));
        }
        else EmptyLabel.Text = Loc.T("Notes.EmptyRuntime");
    }
    private void CategoryWheel(object sender, MouseWheelEventArgs e)
    {
        if (CategoryScroll.ScrollableWidth <= 0) return;
        CategoryScroll.ScrollToHorizontalOffset(CategoryScroll.HorizontalOffset - e.Delta);
        e.Handled = true;
    }
    private void AddCategoryClick(object sender, RoutedEventArgs e)
    {
        var created = PromptCategory(null);
        if (created == null) return;
        manager.SetCategoryFilter(created.Id);
        Refresh();
        RevealActiveTab();
    }
    private void EditCategory(string id)
    {
        var category = manager.Preferences.Categories.FirstOrDefault(c => c.Id == id);
        if (category != null) PromptCategory(category);
    }
    private void RemoveCategory(string id)
    {
        var category = manager.Preferences.Categories.FirstOrDefault(c => c.Id == id);
        if (category == null) return;
        if (MessageBox.Show(this, Loc.F("Categories.DeleteConfirm", ("category.Name", category.Name)), Loc.T("Categories.DeleteTitle"),
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        manager.DeleteCategory(id);
    }
    private NoteCategory? PromptCategory(NoteCategory? existing)
    {
        var color = existing?.Color ?? CategoryThemes.Next(manager.Preferences.Categories.Select(c => c.Color));
        var draft = CategoryDialog.Prompt(this,
            existing == null ? Loc.T("Categories.NewTitle") : Loc.T("Categories.EditTitle"),
            existing == null ? Loc.T("Common.Create") : Loc.T("Common.Save"),
            existing?.Name ?? "", color, name => manager.CategoryNameError(name, existing?.Id));
        if (draft == null) return null;
        if (existing == null) return manager.AddCategory(draft.Value.Name, draft.Value.Color);
        return manager.UpdateCategory(existing.Id, draft.Value.Name, draft.Value.Color) ? existing : null;
    }
    private MenuItem BuildCategoryMenu(Note[] selected)
    {
        var root = new MenuItem { Header = Loc.T("Categories.Label") };
        string? shared = null;
        var same = true;
        foreach (var note in selected)
        {
            var id = manager.CategoryName(note.CategoryId).Length == 0 ? "" : note.CategoryId ?? "";
            if (shared == null) shared = id;
            else if (shared != id) same = false;
        }
        root.Items.Add(CategoryChoice(Loc.T("Categories.Uncategorized"), NoteManager.NoCategory, "#C8C9BE", same && shared == "", selected));
        foreach (var category in manager.Preferences.Categories)
            root.Items.Add(CategoryChoice(category.Name, category.Id, category.Color, same && shared == category.Id, selected));
        root.Items.Add(new Separator());
        var create = new MenuItem { Header = Loc.T("Categories.NewMenu") };
        create.Click += (_, _) =>
        {
            var created = PromptCategory(null);
            if (created != null) manager.AssignCategory(selected, created.Id);
        };
        root.Items.Add(create);
        return root;
    }
    private MenuItem CategoryChoice(string header, string id, string color, bool check, Note[] selected)
    {
        var item = new MenuItem { Header = header, IsCheckable = true, IsChecked = check };
        item.Icon = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Ui.Brush(color) };
        item.Click += (_, _) => manager.AssignCategory(selected, id);
        return item;
    }
    private void RevealActiveTab() => Dispatcher.BeginInvoke(new Action(() =>
    {
        foreach (var child in CategoryBar.Children)
            if (child is RadioButton { IsChecked: true } radio) { radio.BringIntoView(); break; }
    }));
    private void SearchChanged(object s, TextChangedEventArgs e) => RefreshList();
    private void NewClick(object s, RoutedEventArgs e) => manager.Create();
    private void SettingsClick(object s, RoutedEventArgs e) => SettingsRequested?.Invoke();
    private void SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (ExportButton == null || NotesList == null) return;
        ExportButton.IsEnabled = NotesList.SelectedItems.Count > 0;
    }
    private void ImportClick(object s, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = Loc.T("Import.Text"), Filter = Loc.T("Files.TextFilter"), Multiselect = true };
        if (dialog.ShowDialog(this) != true) return;
        var errors = new List<string>(); int imported = 0;
        foreach (var path in dialog.FileNames)
        {
            try { manager.Create(TxtTransfer.Read(path)); imported++; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
            { errors.Add(Path.GetFileName(path) + "：" + (ex is DecoderFallbackException ? Loc.T("Import.Utf8Required") : ex.Message)); }
        }
        Reveal();
        if (errors.Count > 0) MessageBox.Show(this, Loc.F("Import.PartialResultPrefix", ("imported", imported)) + "\n" + string.Join("\n", errors), Loc.T("Import.ResultsTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }
    private void ExportClick(object s, RoutedEventArgs e)
    {
        var notes = NotesList.SelectedItems.Cast<Note>().ToArray(); if (notes.Length == 0) return;
        if (!manager.FlushAll()) { MessageBox.Show(this, Loc.T("Export.UnsavedNotes")); return; }
        try
        {
            if (notes.Length == 1)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { Title = Loc.T("Export.TextButton"), FileName = TxtTransfer.FileStem(notes[0]) + ".txt", Filter = Loc.T("Files.TextFilter"), DefaultExt = ".txt", AddExtension = true, OverwritePrompt = true };
                if (dialog.ShowDialog(this) == true) TxtTransfer.ExportSingle(notes[0], dialog.FileName);
            }
            else
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog { Title = Loc.T("Export.SelectFolder") };
                if (dialog.ShowDialog(this) != true) return;
                int count = 0;
                try { foreach (var note in notes) { TxtTransfer.ExportUnique(note, dialog.FolderName); count++; } }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EncoderFallbackException)
                { MessageBox.Show(this, Loc.F("Export.PartialResultPrefix", ("count", count), ("notes.Length", notes.Length)) + "\n" + ex.Message, Loc.T("Export.IncompleteTitle")); return; }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EncoderFallbackException)
        { MessageBox.Show(this, Loc.T("Export.FailedPrefix") + "\n" + ex.Message, Loc.T("Export.TextButton")); }
    }
    private void NoteClick(object s, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(NotesList, e.OriginalSource as DependencyObject) as ListBoxItem;
        if (item?.DataContext is Note note && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0) manager.Open(note);
    }
    private void NoteKey(object s, KeyEventArgs e) { if (e.Key == Key.Enter && NotesList.SelectedItems.Count == 1 && NotesList.SelectedItem is Note note) { manager.Open(note); e.Handled = true; } }
}
