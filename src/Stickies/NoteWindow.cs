using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;

namespace PaperNotes;

public sealed class NoteWindow : Window
{
    public const double CompactDragHeight = 10;
    public Note Note { get; }
    public NoteEditor Editor { get; }
    private readonly NoteStore store;
    private readonly DispatcherTimer saveTimer;
    private readonly TextBlock status;
    private readonly DockPanel layout;
    private readonly DockPanel bar;
    private readonly Border compactDrag;
    private readonly Border collapsedHost;
    private readonly TextBlock collapsePreview;
    private readonly Button formatButton;
    private readonly Button undoButton;
    private readonly Button moreButton;
    private readonly Button minimizeButton;
    private readonly Button closeButton;
    private Popup? formatPopup;
    private string? statusKey;
    public bool IsCompact => !Note.IsCollapsed && !collapseAnimating && ActualHeight < Note.CompactHeight;
    public bool IsCompactDragVisible => compactDrag.Visibility == Visibility.Visible;
    private bool dirty;
    private bool editorDirty;
    private bool allowClose;
    private bool placing;
    private bool dragging;
    private bool collapseAnimating;
    private bool suppressGeometry;
    private Point dragMouseScreen;
    private Point dragWindowOrigin;
    public Action? NewRequested { get; set; }
    public Action? IndexRequested { get; set; }
    public Action? DeleteRequested { get; set; }
    public event Action<Note>? Changed;
    public NoteWindow(Note note, NoteStore store)
    {
        Note = note; this.store = store;
        AppIcon.Apply(this);
        Title = note.Title;
        Width = note.IsCollapsed ? Note.CollapsedWidth : note.Width;
        Height = note.IsCollapsed ? Note.CollapsedHeight : note.Height;
        Left = note.X; Top = note.Y;
        MinWidth = 180; MinHeight = Note.MinimumHeight; WindowStyle = WindowStyle.None; AllowsTransparency = true; ResizeMode = ResizeMode.CanResize;
        FontFamily = new FontFamily("Yu Gothic UI, Meiryo UI, Microsoft YaHei UI, Segoe UI"); FontSize = 13;
        Background = Ui.Brush(Palette.Colors[note.Color]);
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0) });
        layout = new DockPanel { LastChildFill = true, Margin = new Thickness(6) };
        bar = new DockPanel { Background = Brushes.Transparent, Height = 32, LastChildFill = true };
        // Drag empty chrome only — not the toolbar buttons.
        AttachManualDrag(bar, source => source is DockPanel or Border or TextBlock, onDoubleClick: () => { if (!Note.IsCollapsed) ToggleCollapse(); });
        Topmost = note.IsAlwaysOnTop;
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        undoButton = Ui.Button("↶", Loc.T("Edit.Undo"), (_, _) =>
        {
            CloseFormatPopup();
            Editor.PerformUndo();
            UpdateUndoButton();
            Editor.Focus();
        });
        undoButton.Focusable = false;
        formatButton = Ui.Button("A", Loc.T("Format.Title"), (_, _) => { }); formatButton.Focusable = false;
        formatButton.Click += (_, _) => ToggleFormatPopup();
        moreButton = Ui.Button("···", Loc.T("Note.MoreTooltip"), (_, _) => { });
        moreButton.Click += (_, _) => { CloseFormatPopup(); var menu = BuildMenu(); menu.PlacementTarget = moreButton; menu.IsOpen = true; };
        minimizeButton = Ui.Button("−", Loc.T("Window.Minimize"), (_, _) => WindowState = WindowState.Minimized);
        closeButton = Ui.Button("×", Loc.T("Note.HideTooltip"), (_, _) => Close());
        foreach (var button in new[] { undoButton, formatButton, moreButton, minimizeButton, closeButton })
        {
            button.Padding = new Thickness(6, 3, 6, 3);
            button.Margin = new Thickness(3, 0, 3, 0);
            button.MinWidth = 0;
            tools.Children.Add(button);
        }
        DockPanel.SetDock(tools, Dock.Right);
        bar.Children.Add(tools);
        bar.Children.Add(new Border { Background = Brushes.Transparent });
        DockPanel.SetDock(bar, Dock.Top); layout.Children.Add(bar);
        compactDrag = new Border
        {
            Height = CompactDragHeight, Background = Brushes.Transparent, Visibility = Visibility.Collapsed,
            Cursor = Cursors.SizeAll, IsHitTestVisible = true
        };
        AttachManualDrag(compactDrag, _ => true, onDoubleClick: () => { if (!Note.IsCollapsed) ToggleCollapse(); });
        DockPanel.SetDock(compactDrag, Dock.Top); layout.Children.Add(compactDrag);
        status = new TextBlock { FontSize = 11, Foreground = Ui.Brush("#A63C32"), TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed, Margin = new Thickness(12, 2, 12, 4) };
        DockPanel.SetDock(status, Dock.Bottom); layout.Children.Add(status);
        Editor = new NoteEditor(note) {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            Foreground = Ui.Brush("#34342F"), FontSize = 16, Padding = new Thickness(12, 10, 12, 10), IsUndoEnabled = true, UndoLimit = 100,
            // Prefer CJK-capable fonts so pasted kanji are not blank while data is intact.
            FontFamily = new FontFamily("Yu Gothic UI, Meiryo UI, Microsoft YaHei UI, Segoe UI") };
        layout.Children.Add(Editor);

        collapsePreview = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Foreground = Ui.Brush("#34342F"),
            FontSize = 16,
            FontFamily = new FontFamily("Microsoft YaHei UI")
        };
        collapsedHost = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = Brushes.Transparent,
            ClipToBounds = true,
            Padding = new Thickness(14, 0, 14, 0),
            Cursor = Cursors.SizeAll,
            Child = collapsePreview
        };
        AttachManualDrag(collapsedHost, _ => true, onDoubleClick: () => { if (Note.IsCollapsed) ToggleCollapse(); });

        var root = new Grid();
        root.Children.Add(layout);
        root.Children.Add(collapsedHost);
        Content = root;

        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) => Flush();
        Editor.TextChanged += (_, _) =>
        {
            var text = Editor.Text;
            if (text != NoteEditor.Normalize(Note.Content)) Note.Content = text;
            editorDirty = true; Note.UpdatedAt = DateTime.UtcNow; Title = Note.Title; MarkDirty();
            UpdateUndoButton();
        };
        UpdateUndoButton();
        Editor.ContextMenu = new ContextMenu();
        Editor.ContextMenuOpening += (_, _) =>
        {
            var menu = Editor.ContextMenu; menu.Items.Clear();
            foreach (var pair in new (string, RoutedUICommand)[] {
                (Loc.T("Edit.Cut"), ApplicationCommands.Cut),
                (Loc.T("Edit.Copy"), ApplicationCommands.Copy),
                (Loc.T("Edit.Paste"), ApplicationCommands.Paste),
                (Loc.T("Edit.SelectAll"), ApplicationCommands.SelectAll) })
                menu.Items.Add(new MenuItem { Header = pair.Item1, Command = pair.Item2, CommandTarget = Editor });
            menu.Items.Add(new Separator());
            var formatting = new MenuItem { Header = Loc.T("Format.Title") };
            var formatMenu = Editor.FormatMenu();
            while (formatMenu.Items.Count > 0) { var item = formatMenu.Items[0]; formatMenu.Items.RemoveAt(0); formatting.Items.Add(item); }
            menu.Items.Add(formatting);
            if (IsCompact)
            {
                menu.Items.Add(new Separator()); var noteMenu = BuildMenu();
                while (noteMenu.Items.Count > 0) { var item = noteMenu.Items[0]; noteMenu.Items.RemoveAt(0); menu.Items.Add(item); }
            }
        };
        if (Editor.LoadWarning != null) { status.Text = Editor.LoadWarning; status.Visibility = Visibility.Visible; }
        Loc.Changed += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            EnsureVisible();
            if (Note.IsCollapsed) ApplyCollapsedChrome(immediate: true);
            else UpdateCompact();
            if (!Note.IsCollapsed) Editor.Focus();
        };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WindowHook);
            WindowEffects.DisableSnapMaximize(hwnd);
            WindowEffects.SetNoteOpacity(this, Note.IsTranslucent);
        };
        // Reassert topmost after WPF visibility/activation transitions, without taking focus.
        Deactivated += (_, _) => { CloseFormatPopup(); QueueTopmost(); };
        Activated += (_, _) => { ApplyTopmost(); WindowEffects.BringToFront(this); };
        PreviewMouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) WindowEffects.BringToFront(this); };
        IsVisibleChanged += (_, _) => QueueTopmost();
        StateChanged += (_, _) =>
        {
            // Aero Snap can still force Maximized; restore Normal without changing intended size.
            if (WindowState == WindowState.Maximized) { WindowState = WindowState.Normal; Width = Note.Width; Height = Note.Height; }
            QueueTopmost();
        };
        LocationChanged += (_, _) => GeometryChanged();
        SizeChanged += (_, _) => { UpdateCompact(); GeometryChanged(); };
        PreviewMouseLeftButtonDown += FormatPopupOutsideClick;
        Closing += (_, e) =>
        {
            if (allowClose) return;
            e.Cancel = true; Note.IsHidden = true; MarkDirty();
            if (Flush()) Hide(); else { Note.IsHidden = false; MarkDirty(); }
        };
        Closed += (_, _) => Loc.Changed -= OnLanguageChanged;
    }
    public bool IsFormatPopupOpen => formatPopup?.IsOpen == true;
    public UIElement? FormatPopupChildForTest => formatPopup?.Child;
    public void ToggleFormatPopupForTest() => ToggleFormatPopup();
    public void ToggleCollapseForTest() => ToggleCollapse();
    void UpdateUndoButton() => undoButton.IsEnabled = Editor.HasUndo;
    private void ToggleFormatPopup()
    {
        if (formatPopup?.IsOpen == true) { CloseFormatPopup(); return; }
        Editor.RememberSelection();
        formatPopup ??= new Popup
        {
            StaysOpen = true,
            AllowsTransparency = true,
            Placement = PlacementMode.Bottom,
            HorizontalOffset = -4,
            VerticalOffset = 4,
        };
        formatPopup.PlacementTarget = formatButton;
        RefreshFormatPopupContent();
        formatPopup.IsOpen = true;
        // Do not Focus() the editor here — that dismisses ContextMenu and can race Popup.
    }
    private void RefreshFormatPopupContent()
    {
        if (formatPopup == null) return;
        // Defer rebuild: replacing Child inside Click can deliver a second Click to the new chip
        // (font size then jumps two steps).
        formatPopup.Child = Editor.BuildFormatPopupContent(() =>
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (formatPopup?.IsOpen == true) RefreshFormatPopupContent();
            })));
    }
    private void CloseFormatPopup()
    {
        if (formatPopup != null) formatPopup.IsOpen = false;
    }
    private void FormatPopupOutsideClick(object sender, MouseButtonEventArgs e)
    {
        if (formatPopup?.IsOpen != true) return;
        if (e.OriginalSource is not DependencyObject source) return;
        if (IsUnder(formatPopup.Child, source) || IsUnder(formatButton, source)) return;
        CloseFormatPopup();
    }
    private static bool IsUnder(DependencyObject? root, DependencyObject? node)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, root)) return true;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return false;
    }
    private void UpdateCompact()
    {
        // Skip Compact Mode while collapsed or mid-animation (height crosses CompactHeight).
        if (Note.IsCollapsed || collapseAnimating) return;
        bool compact = ActualHeight < Note.CompactHeight;
        bar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        compactDrag.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        if (compact) CloseFormatPopup();
        Editor.Padding = compact ? new Thickness(6, 0, 6, 2) : new Thickness(12, 10, 12, 10);
        Editor.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;
    }
    private void ToggleCollapse()
    {
        if (collapseAnimating) return;
        if (Note.IsCollapsed) BeginExpand();
        else BeginCollapse();
    }
    private void BeginCollapse()
    {
        if (collapseAnimating || Note.IsCollapsed) return;
        CloseFormatPopup();
        collapseAnimating = true;
        suppressGeometry = true;
        ClearSizeAnimations();

        Note.ExpandedWidth = ActualWidth > 0 ? ActualWidth : Width;
        Note.ExpandedHeight = ActualHeight > 0 ? ActualHeight : Height;
        RefreshCollapsePreview();

        collapsedHost.Opacity = 0;
        collapsedHost.Visibility = Visibility.Visible;
        layout.Opacity = 1;

        var sizeDuration = TimeSpan.FromMilliseconds(150);
        var fadeDuration = TimeSpan.FromMilliseconds(100);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        layout.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, fadeDuration) { EasingFunction = ease });
        collapsedHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, fadeDuration) { EasingFunction = ease });

        AnimateSize(Note.CollapsedWidth, Note.CollapsedHeight, sizeDuration, ease, () =>
        {
            layout.BeginAnimation(OpacityProperty, null);
            collapsedHost.BeginAnimation(OpacityProperty, null);
            layout.Opacity = 1;
            collapsedHost.Opacity = 1;
            layout.Visibility = Visibility.Collapsed;
            Note.IsCollapsed = true;
            Note.Width = Note.CollapsedWidth;
            Note.Height = Note.CollapsedHeight;
            ApplyCollapsedChrome(immediate: false);
            collapseAnimating = false;
            suppressGeometry = false;
            GeometryChanged();
            MarkDirty();
            Flush();
        });
    }
    private void BeginExpand()
    {
        if (collapseAnimating || !Note.IsCollapsed) return;
        collapseAnimating = true;
        suppressGeometry = true;
        ClearSizeAnimations();

        double toW = Note.ExpandedWidth >= 180 ? Note.ExpandedWidth : 300;
        double toH = Note.ExpandedHeight >= Note.MinimumHeight ? Note.ExpandedHeight : 300;

        // Unlock MinWidth/MinHeight before animating larger.
        MaxWidth = double.PositiveInfinity;
        MaxHeight = double.PositiveInfinity;
        MinWidth = 180;
        MinHeight = Note.MinimumHeight;
        ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0)
        });

        layout.Opacity = 0;
        layout.Visibility = Visibility.Visible;
        collapsedHost.Opacity = 1;
        Note.IsCollapsed = false;

        var sizeDuration = TimeSpan.FromMilliseconds(150);
        var fadeDuration = TimeSpan.FromMilliseconds(100);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

        collapsedHost.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, fadeDuration) { EasingFunction = ease });
        layout.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, fadeDuration) { EasingFunction = ease });

        AnimateSize(toW, toH, sizeDuration, ease, () =>
        {
            layout.BeginAnimation(OpacityProperty, null);
            collapsedHost.BeginAnimation(OpacityProperty, null);
            layout.Opacity = 1;
            collapsedHost.Opacity = 1;
            collapsedHost.Visibility = Visibility.Collapsed;
            collapsePreview.Inlines.Clear();
            Note.Width = toW;
            Note.Height = toH;
            collapseAnimating = false;
            suppressGeometry = false;
            UpdateCompact();
            GeometryChanged();
            MarkDirty();
            Flush();
            Editor.Focus();
        });
    }
    private void ApplyCollapsedChrome(bool immediate)
    {
        RefreshCollapsePreview();
        layout.Visibility = Visibility.Collapsed;
        collapsedHost.Visibility = Visibility.Visible;
        collapsedHost.Opacity = 1;
        Width = Note.CollapsedWidth;
        Height = Note.CollapsedHeight;
        MinWidth = Note.CollapsedWidth;
        MinHeight = Note.CollapsedHeight;
        MaxWidth = Note.CollapsedWidth;
        MaxHeight = Note.CollapsedHeight;
        ResizeMode = ResizeMode.NoResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0, ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0)
        });
        Note.Width = Note.CollapsedWidth;
        Note.Height = Note.CollapsedHeight;
        if (immediate)
        {
            Note.IsCollapsed = true;
            GeometryChanged();
        }
    }
    private void RefreshCollapsePreview() => CollapsePreview.Fill(collapsePreview, Editor.Document);
    private void AnimateSize(double toW, double toH, TimeSpan duration, IEasingFunction ease, Action onComplete)
    {
        int remaining = 2;
        EventHandler done = (_, _) =>
        {
            if (--remaining != 0) return;
            ClearSizeAnimations();
            Width = toW;
            Height = toH;
            onComplete();
        };
        var wa = new DoubleAnimation(Width, toW, duration) { EasingFunction = ease };
        var ha = new DoubleAnimation(Height, toH, duration) { EasingFunction = ease };
        wa.Completed += done;
        ha.Completed += done;
        BeginAnimation(WidthProperty, wa);
        BeginAnimation(HeightProperty, ha);
    }
    private void ClearSizeAnimations()
    {
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
    }
    private void AttachManualDrag(UIElement target, Func<object, bool> allowSource, Action? onDoubleClick = null)
    {
        target.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left || !allowSource(e.OriginalSource)) return;
            if (e.ClickCount > 1)
            {
                if (onDoubleClick != null) { onDoubleClick(); e.Handled = true; }
                return;
            }
            // Manual Left/Top updates avoid DragMove's SC_MOVE loop, which triggers Aero Snap.
            dragging = true;
            dragMouseScreen = PointToScreen(e.GetPosition(this));
            dragWindowOrigin = new Point(Left, Top);
            target.CaptureMouse();
            e.Handled = true;
        };
        target.MouseMove += (_, e) =>
        {
            if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
            var now = PointToScreen(e.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget == null) return;
            var delta = source.CompositionTarget.TransformFromDevice.Transform(new Point(now.X - dragMouseScreen.X, now.Y - dragMouseScreen.Y));
            Left = dragWindowOrigin.X + delta.X;
            Top = dragWindowOrigin.Y + delta.Y;
        };
        target.MouseLeftButtonUp += (_, e) =>
        {
            if (!dragging) return;
            dragging = false; target.ReleaseMouseCapture();
            GeometryChanged(); Flush(); e.Handled = true;
        };
        target.LostMouseCapture += (_, _) => { if (dragging) { dragging = false; GeometryChanged(); Flush(); } };
    }
    public void SetTranslucent(bool value)
    {
        WindowEffects.SetNoteOpacity(this, value);
        Note.IsTranslucent = value; MarkDirty(); Flush();
    }
    private void GeometryChanged()
    {
        if (placing || suppressGeometry || collapseAnimating || !IsLoaded || WindowState != WindowState.Normal) return;
        WindowPlacement.Capture(this, Note);
        if (Note.IsCollapsed)
        {
            Note.Width = Note.CollapsedWidth;
            Note.Height = Note.CollapsedHeight;
        }
        else
        {
            Note.ExpandedWidth = Note.Width;
            Note.ExpandedHeight = Note.Height;
        }
        MarkDirty();
    }
    private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        const int wmSysCommand = 0x0112;
        const int wmExitSizeMove = 0x0232;
        const int wmWindowPosChanging = 0x0046;
        const int scMaximize = 0xF030;
        if (message == wmSysCommand)
        {
            int cmd = wp.ToInt32() & 0xFFF0;
            // Block only maximize (Snap). Never block SC_RESTORE — taskbar thumbnails send it to un-minimize.
            if (cmd == scMaximize) { handled = true; return IntPtr.Zero; }
        }
        else if (message == wmWindowPosChanging && dragging && !placing)
        {
            // Only while our manual drag is active: reject Snap rewriting WIDTH/HEIGHT.
            var pos = Marshal.PtrToStructure<WindowPos>(lp);
            const int swpNosize = 0x0001;
            if ((pos.Flags & swpNosize) == 0 && WindowState == WindowState.Normal)
            {
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1;
                pos.Cx = (int)Math.Round(ActualWidth * dpiX);
                pos.Cy = (int)Math.Round(ActualHeight * dpiY);
                Marshal.StructureToPtr(pos, lp, false);
            }
        }
        else if (message == wmExitSizeMove) { GeometryChanged(); Flush(); }
        return IntPtr.Zero;
    }
    public void EnsureVisible()
    {
        placing = true;
        try { WindowPlacement.Restore(this, Note); }
        finally { placing = false; }
        GeometryChanged();
        ApplyTopmost();
    }
    public void RestoreAndActivate(bool focusEditor = true)
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        WindowEffects.TryForeground(this);
        if (focusEditor && !Note.IsCollapsed)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Editor.Focus()));
    }
    private void QueueTopmost() => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { if (!allowClose) ApplyTopmost(); }));
    private void ApplyTopmost() => WindowEffects.ApplyTopmost(this, Note.IsAlwaysOnTop);
    public void MarkDirty() { dirty = true; saveTimer.Stop(); saveTimer.Interval = TimeSpan.FromMilliseconds(400); saveTimer.Start(); }
    public void Destroy() { saveTimer.Stop(); dirty = false; allowClose = true; Close(); }
    public void SetTopmost(bool value) { Note.IsAlwaysOnTop = value; ApplyTopmost(); MarkDirty(); Flush(); }
    public void SetPinned(bool value) { Note.IsPinned = value; MarkDirty(); Flush(); }
    public void SetColor(string color) { Note.Color = color; Background = Ui.Brush(Palette.Colors[color]); MarkDirty(); Flush(); }
    void OnLanguageChanged()
    {
        CloseFormatPopup();
        if (Editor.ContextMenu != null) Editor.ContextMenu.IsOpen = false;
        Title = Note.Title;
        undoButton.ToolTip = Loc.T("Edit.Undo");
        formatButton.ToolTip = Loc.T("Format.Title");
        moreButton.ToolTip = Loc.T("Note.MoreTooltip");
        minimizeButton.ToolTip = Loc.T("Window.Minimize");
        closeButton.ToolTip = Loc.T("Note.HideTooltip");
        if (statusKey != null)
        {
            status.Text = Loc.T(statusKey);
            status.Visibility = Visibility.Visible;
        }
        else if (Editor.LoadWarning != null)
        {
            status.Text = Editor.LoadWarning;
            status.Visibility = Visibility.Visible;
        }
    }
    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var top = new MenuItem { Header = Loc.T("Note.AlwaysOnTop"), IsCheckable = true, IsChecked = Note.IsAlwaysOnTop };
        top.Click += (_, _) => SetTopmost(top.IsChecked); menu.Items.Add(top);
        var translucent = new MenuItem { Header = Loc.T("Note.Translucent"), IsCheckable = true, IsChecked = Note.IsTranslucent };
        translucent.Click += (_, _) => SetTranslucent(translucent.IsChecked); menu.Items.Add(translucent);
        var pin = new MenuItem { Header = Loc.T("Note.PinInList"), IsCheckable = true, IsChecked = Note.IsPinned };
        pin.Click += (_, _) => SetPinned(pin.IsChecked); menu.Items.Add(pin);
        var colors = new MenuItem { Header = Loc.T("Note.Color") };
        foreach (var pair in Palette.Colors)
        {
            var item = new MenuItem { Header = Palette.DisplayName(pair.Key), IsCheckable = true, IsChecked = Note.Color == pair.Key,
                Icon = new System.Windows.Shapes.Rectangle { Width = 14, Height = 14, Fill = Ui.Brush(pair.Value) } };
            item.Click += (_, _) => SetColor(pair.Key); colors.Items.Add(item);
        }
        menu.Items.Add(colors); menu.Items.Add(new Separator());
        var index = new MenuItem { Header = Loc.T("Notes.OpenListMenu") }; index.Click += (_, _) => IndexRequested?.Invoke(); menu.Items.Add(index);
        var hide = new MenuItem { Header = Loc.T("Note.HideMenu") }; hide.Click += (_, _) => Close(); menu.Items.Add(hide);
        var delete = new MenuItem { Header = Loc.T("Note.DeleteMenu") }; delete.Click += (_, _) => DeleteRequested?.Invoke(); menu.Items.Add(delete);
        return menu;
    }
    public bool Flush()
    {
        saveTimer.Stop();
        if (!dirty) return true;
        try
        {
            Editor.ApplyCommittedTypingStyle();
            if (editorDirty) { Note.RichTextRtfBase64 = Editor.SaveRtf(); Note.FormatVersion = 2; }
            store.Save(Note); dirty = false; editorDirty = false;
            statusKey = null;
            status.Text = ""; status.Visibility = Visibility.Collapsed; Changed?.Invoke(Note); return true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            statusKey = "Note.SaveRetrying";
            status.Text = Loc.T(statusKey); status.Visibility = Visibility.Visible; status.ToolTip = ex.Message;
            saveTimer.Interval = TimeSpan.FromSeconds(2); saveTimer.Start(); return false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        public IntPtr Hwnd; public IntPtr HwndInsertAfter;
        public int X; public int Y; public int Cx; public int Cy; public int Flags;
    }
}
