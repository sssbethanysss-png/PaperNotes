using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PaperNotes;

public sealed class SettingsWindow : Window
{
    private readonly Preferences preferences;
    private readonly SettingsStore store;
    private readonly Hotkeys hotkeys;
    private readonly Action? bindingsChanged;
    private CheckBox startup = null!;
    private CheckBox top = null!;
    private ComboBox colors = null!;
    private ComboBox language = null!;
    private HotkeyField createField = null!;
    private HotkeyField toggleField = null!;
    private readonly TextBlock error;
    private string draftLanguage;
    private bool wasStartupEnabled;
    private bool saving;

    public SettingsWindow(Preferences preferences, SettingsStore store, Hotkeys hotkeys, Action? bindingsChanged = null)
    {
        this.preferences = preferences;
        this.store = store;
        this.hotkeys = hotkeys;
        this.bindingsChanged = bindingsChanged;
        draftLanguage = Loc.NormalizeLanguage(preferences.UiLanguage);
        Width = 520;
        Height = 620;
        MinWidth = 440;
        MinHeight = 420;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        AppIcon.Apply(this);
        FontFamily = new FontFamily("Yu Gothic UI, Meiryo UI, Microsoft YaHei UI, Segoe UI");
        FontSize = 13;
        Background = Ui.Brush("#FAF9F6");
        Foreground = Ui.Brush("#35372F");

        var body = new DockPanel { Margin = new Thickness(0) };
        Content = body;

        var footer = new Border
        {
            Background = Ui.Brush("#FAF9F6"),
            BorderBrush = Ui.Brush("#E4E3DB"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14, 24, 16)
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        var footerRow = new DockPanel();
        footer.Child = footerRow;
        error = new TextBlock
        {
            Foreground = Ui.Brush("#AA4545"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        footerRow.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(actions, Dock.Right);
        var cancel = SoftButton(Loc.T("Common.Cancel"), "#F1F0EA", false);
        cancel.IsCancel = true;
        cancel.Click += (_, _) =>
        {
            if (saving) return;
            HotkeyField.CancelActiveRecording();
            Close();
        };
        cancel.Margin = new Thickness(0, 0, 8, 0);
        var save = SoftButton(Loc.T("Settings.Save"), "#DDE6CB", true);
        save.IsDefault = true;
        save.Click += (_, _) => Save();
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        footerRow.Children.Add(actions);
        body.Children.Add(footer);

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(24, 20, 24, 8)
        };
        var panel = new StackPanel();
        scroll.Content = panel;
        body.Children.Add(scroll);

        panel.Children.Add(new TextBlock
        {
            Text = Loc.T("Settings.Subtitle"),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 18),
            TextWrapping = TextWrapping.Wrap
        });

        panel.Children.Add(SectionCard(Loc.T("Settings.General"), general =>
        {
            language = new ComboBox { Height = 34, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 0, 12) };
            AddLanguage(Loc.Ja, Loc.T("Settings.LanguageJapanese"));
            AddLanguage(Loc.Zh, Loc.T("Settings.LanguageChinese"));
            AddLanguage(Loc.En, Loc.T("Settings.LanguageEnglish"));
            language.SelectionChanged += (_, _) =>
            {
                if (language.SelectedItem is ComboBoxItem { Tag: string code })
                    draftLanguage = Loc.NormalizeLanguage(code);
            };
            general.Children.Add(new TextBlock
            {
                Text = Loc.T("Settings.Language"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 6)
            });
            general.Children.Add(new TextBlock
            {
                Text = Loc.T("Settings.LanguageHelp"),
                Foreground = Ui.Brush("#858878"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            general.Children.Add(language);

            startup = new CheckBox { Content = Loc.T("Settings.LaunchAtLogin"), Margin = new Thickness(0, 2, 0, 0) };
            try { wasStartupEnabled = Startup.Enabled; startup.IsChecked = wasStartupEnabled; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                wasStartupEnabled = false;
                startup.IsEnabled = false;
                startup.ToolTip = ex.Message;
            }
            general.Children.Add(Labeled(startup, Loc.T("Settings.LaunchAtLoginHelp")));
        }));

        panel.Children.Add(SectionCard(Loc.T("Settings.Appearance"), appearance =>
        {
            top = new CheckBox { Content = Loc.T("Settings.NewNotesOnTop"), IsChecked = preferences.DefaultAlwaysOnTop };
            appearance.Children.Add(Labeled(top, Loc.T("Settings.NewNotesOnly")));
            appearance.Children.Add(new TextBlock
            {
                Text = Loc.T("Settings.DefaultNoteColor"),
                Margin = new Thickness(0, 14, 0, 6),
                FontWeight = FontWeights.SemiBold
            });
            appearance.Children.Add(new TextBlock
            {
                Text = Loc.T("Settings.DefaultColorHelp"),
                Foreground = Ui.Brush("#858878"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });
            colors = new ComboBox { Height = 34, Padding = new Thickness(8, 4, 8, 4) };
            foreach (var pair in Palette.Colors)
            {
                var item = new ComboBoxItem { Content = Palette.DisplayName(pair.Key), Tag = pair.Key, Background = Ui.Brush(pair.Value) };
                colors.Items.Add(item);
                if (pair.Key == preferences.DefaultColor) colors.SelectedItem = item;
            }
            if (colors.SelectedItem == null && colors.Items.Count > 0) colors.SelectedIndex = 0;
            appearance.Children.Add(colors);
        }));

        panel.Children.Add(SectionCard(Loc.T("Settings.Shortcuts"), keys =>
        {
            createField = new HotkeyField(Loc.T("Hotkeys.CreateAction"), preferences.HotkeyCreate);
            toggleField = new HotkeyField(Loc.T("Hotkeys.ToggleAllAction"), preferences.HotkeyToggleAll);
            keys.Children.Add(createField);
            keys.Children.Add(new Border { Height = 14 });
            keys.Children.Add(toggleField);
        }));

        Title = Loc.T("Settings.WindowTitle");
        TitleBar.Attach(this, Loc.BrandName, showMaximize: false);
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnWindowPreviewKeyDown), handledEventsToo: true);
        Closed += (_, _) => HotkeyField.CancelActiveRecording();
    }

    void AddLanguage(string code, string label)
    {
        var item = new ComboBoxItem { Content = label, Tag = code };
        language.Items.Add(item);
        if (code == draftLanguage) language.SelectedItem = item;
    }

    void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HotkeyField.Active is not { } field) return;
        if (field.TryHandleKey(e))
            e.Handled = true;
    }

    void Save()
    {
        if (HotkeyField.Active != null)
            HotkeyField.CancelActiveRecording();
        saving = true;
        error.Text = "";
        string? draftCreate = createField.DraftChord;
        string? draftToggle = toggleField.DraftChord;
        if (!HotkeyChord.IsUnset(draftCreate) && string.Equals(draftCreate, draftToggle, StringComparison.OrdinalIgnoreCase))
        {
            error.Text = Loc.T("Hotkeys.Duplicate");
            saving = false;
            return;
        }

        string oldColor = preferences.DefaultColor;
        bool oldTop = preferences.DefaultAlwaysOnTop;
        string? oldCreate = preferences.HotkeyCreate;
        string? oldToggle = preferences.HotkeyToggleAll;
        string oldLanguage = Loc.NormalizeLanguage(preferences.UiLanguage);
        string nextLanguage = Loc.NormalizeLanguage(draftLanguage);

        var applied = hotkeys.Apply(draftCreate, draftToggle);
        if (!applied.Ok)
        {
            error.Text = string.Join("\n", applied.Failures);
            saving = false;
            return;
        }

        try
        {
            if (startup.IsEnabled && startup.IsChecked == true != wasStartupEnabled)
                Startup.Set(startup.IsChecked == true);
            preferences.DefaultColor = (string)((ComboBoxItem)colors.SelectedItem).Tag;
            preferences.DefaultAlwaysOnTop = top.IsChecked == true;
            preferences.HotkeyCreate = draftCreate;
            preferences.HotkeyToggleAll = draftToggle;
            preferences.UiLanguage = nextLanguage;
            store.Save(preferences);
            Loc.SetLanguage(nextLanguage);
            bindingsChanged?.Invoke();
            Close();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            preferences.DefaultColor = oldColor;
            preferences.DefaultAlwaysOnTop = oldTop;
            preferences.HotkeyCreate = oldCreate;
            preferences.HotkeyToggleAll = oldToggle;
            preferences.UiLanguage = oldLanguage;
            hotkeys.Apply(oldCreate, oldToggle);
            try { if (startup.IsEnabled && Startup.Enabled != wasStartupEnabled) Startup.Set(wasStartupEnabled); }
            catch { /* keep displaying the save failure */ }
            error.Text = Loc.T("Settings.SaveFailedPrefix") + ex.Message;
            saving = false;
        }
    }

    static Border SectionCard(string title, Action<StackPanel> fill)
    {
        var panel = new StackPanel();
        fill(panel);
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ui.Brush("#5C5F56"),
            Margin = new Thickness(2, 0, 0, 10)
        });
        stack.Children.Add(panel);
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = Ui.Brush("#E4E3DB"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 16),
            Margin = new Thickness(0, 0, 0, 14),
            Child = stack
        };
    }

    static StackPanel Labeled(UIElement control, string hint)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        stack.Children.Add(control);
        stack.Children.Add(new TextBlock
        {
            Text = hint,
            Foreground = Ui.Brush("#858878"),
            FontSize = 12,
            Margin = new Thickness(22, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        return stack;
    }

    static Button SoftButton(string text, string fill, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = primary ? 104 : 88,
            Height = 34,
            Padding = new Thickness(16, 0, 16, 0),
            Cursor = Cursors.Hand,
            Background = Ui.Brush(fill),
            Foreground = Ui.Brush("#35372F"),
            BorderThickness = new Thickness(0),
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            FocusVisualStyle = null
        };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        return button;
    }
}

public sealed class HotkeyField : StackPanel
{
    public static HotkeyField? Active { get; private set; }
    readonly TextBlock valueLabel;
    readonly Border valueChrome;
    readonly Button editButton;
    readonly Button clearButton;
    string? draftBeforeRecording;
    public string? DraftChord { get; private set; }
    public bool IsRecording => Active == this;

    public HotkeyField(string actionName, string? chord)
    {
        DraftChord = HotkeyChord.IsUnset(chord) ? null : chord!.Trim();
        Children.Add(new TextBlock
        {
            Text = actionName,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        });

        var row = new DockPanel { LastChildFill = true };
        Children.Add(row);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0) };
        DockPanel.SetDock(buttons, Dock.Right);
        editButton = SoftChip(HotkeyChord.IsUnset(DraftChord) ? Loc.T("Hotkeys.Set") : Loc.T("Hotkeys.Change"));
        editButton.Click += (_, _) => BeginRecording();
        editButton.ToolTip = Loc.T("Hotkeys.RecordHint");
        clearButton = SoftChip(Loc.T("Common.Clear"));
        clearButton.Margin = new Thickness(6, 0, 0, 0);
        clearButton.ToolTip = Loc.T("Hotkeys.ClearTooltip");
        clearButton.Click += (_, _) => ClearDraft();
        buttons.Children.Add(editButton);
        buttons.Children.Add(clearButton);
        row.Children.Add(buttons);

        valueLabel = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 12, 0),
            IsHitTestVisible = false
        };
        valueChrome = new Border
        {
            Background = Brushes.White,
            BorderBrush = Ui.Brush("#E4E3DB"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Height = 34,
            Child = valueLabel,
            Focusable = false
        };
        row.Children.Add(valueChrome);
        Refresh();
    }

    public static void CancelActiveRecording()
    {
        Active?.CancelRecording(restore: true);
    }

    /// <summary>Clears the draft only; live RegisterHotKey stays until Settings is saved.</summary>
    public void ClearDraft()
    {
        CancelActiveRecording();
        DraftChord = null;
        Refresh();
    }

    public bool TryCommitChord(ModifierKeys mods, Key key)
    {
        if (Active != this) return false;
        // Modifier-only: keep recording; do not commit yet.
        if (HotkeyChord.IsModifierKey(key)) return true;
        if (!HotkeyChord.IsValidGlobal(mods, key, out _)) return true;
        DraftChord = HotkeyChord.Format(mods, key);
        FinishRecording();
        return true;
    }

    public void BeginRecording()
    {
        if (Active != null && Active != this)
            Active.CancelRecording(restore: true);
        draftBeforeRecording = DraftChord;
        Active = this;
        valueChrome.BorderBrush = Ui.Brush("#7C9A62");
        valueChrome.BorderThickness = new Thickness(2);
        valueChrome.Background = Ui.Brush("#F4F7EE");
        valueLabel.Text = HotkeyChord.RecordingHint;
        valueLabel.Foreground = Ui.Brush("#3F4A34");
        valueLabel.FontWeight = FontWeights.SemiBold;
        editButton.IsEnabled = false;
        clearButton.IsEnabled = false;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (Active == this && Window.GetWindow(this) is Window host)
            {
                host.Activate();
                Keyboard.Focus(host);
            }
        }));
    }

    public bool TryHandleKey(KeyEventArgs e)
    {
        if (Active != this) return false;
        var key = HotkeyChord.NormalizeKey(e.Key, e.SystemKey);
        if (key == Key.Escape)
        {
            CancelFromEscape();
            return true;
        }
        var mods = HotkeyChord.CurrentModifiers();
        if (e.Key == Key.System || e.SystemKey != Key.None)
            mods |= ModifierKeys.Alt;
        return TryCommitChord(mods, key);
    }

    public void CancelFromEscape() => CancelRecording(restore: true);

    void CancelRecording(bool restore)
    {
        if (Active != this) return;
        if (restore) DraftChord = draftBeforeRecording;
        FinishRecording();
    }

    void FinishRecording()
    {
        if (Active == this) Active = null;
        editButton.IsEnabled = true;
        clearButton.IsEnabled = true;
        valueChrome.BorderThickness = new Thickness(1);
        valueChrome.BorderBrush = Ui.Brush("#E4E3DB");
        valueChrome.Background = Brushes.White;
        valueLabel.FontWeight = FontWeights.Normal;
        Refresh();
    }

    void Refresh()
    {
        valueLabel.Text = HotkeyChord.Display(DraftChord);
        valueLabel.Foreground = HotkeyChord.IsUnset(DraftChord) ? Ui.Brush("#92958B") : Ui.Brush("#35372F");
        editButton.Content = HotkeyChord.IsUnset(DraftChord) ? Loc.T("Hotkeys.Set") : Loc.T("Hotkeys.Change");
    }

    static Button SoftChip(string text) => new()
    {
        Content = text,
        MinWidth = 64,
        Height = 34,
        Padding = new Thickness(12, 0, 12, 0),
        Cursor = Cursors.Hand,
        Background = Ui.Brush("#F1F0EA"),
        Foreground = Ui.Brush("#35372F"),
        BorderThickness = new Thickness(0),
        FocusVisualStyle = null,
        Template = ChipTemplate()
    };

    static ControlTemplate ChipTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
        });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }
}
