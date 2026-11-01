using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PaperNotes;

/// <summary>Paragraph-attached checklist state. One task == one Paragraph (not visual lines).</summary>
public static class TaskParagraph
{
    public static readonly DependencyProperty IsTaskProperty = DependencyProperty.RegisterAttached(
        "IsTask", typeof(bool), typeof(TaskParagraph),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange, OnIsTaskChanged));
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.RegisterAttached(
        "IsChecked", typeof(bool), typeof(TaskParagraph),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsCheckedChanged));
    /// <summary>Fired when checklist metadata changes (note autosave).</summary>
    public static event Action? Changed;
    public static bool SuppressNotify;
    public static bool GetIsTask(DependencyObject d) => (bool)d.GetValue(IsTaskProperty);
    public static void SetIsTask(DependencyObject d, bool value) => d.SetValue(IsTaskProperty, value);
    public static bool GetIsChecked(DependencyObject d) => (bool)d.GetValue(IsCheckedProperty);
    public static void SetIsChecked(DependencyObject d, bool value) => d.SetValue(IsCheckedProperty, value);

    static void OnIsTaskChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Paragraph paragraph) return;
        bool on = (bool)e.NewValue;
        // During XamlReader.Parse, mutating Inlines throws "collection was modified".
        // Rehydrate applies markers after the document tree is fully loaded.
        if (!SuppressNotify)
        {
            if (on)
            {
                ClearBodyCharacterFormatting(paragraph);
                EnsureMarker(paragraph);
                ApplyLayout(paragraph, true);
                ApplyCheckedLook(paragraph);
            }
            else
            {
                RemoveMarker(paragraph);
                SetIsChecked(paragraph, false);
                ApplyLayout(paragraph, false);
                ApplyCheckedLook(paragraph);
            }
        }
        SyncTag(paragraph);
        if (!SuppressNotify) Changed?.Invoke();
    }
    static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Paragraph paragraph) return;
        if (!SuppressNotify)
        {
            if (FindMarker(paragraph) is TaskMarker marker) marker.SetChecked((bool)e.NewValue);
            ApplyCheckedLook(paragraph);
        }
        SyncTag(paragraph);
        if (!SuppressNotify) Changed?.Invoke();
    }
    static void SyncTag(Paragraph paragraph)
    {
        // XamlWriter reliably persists Tag; attached DPs are best-effort and rehydrated from Tag.
        if (!GetIsTask(paragraph))
        {
            if (paragraph.Tag is string s && s.StartsWith("PaperNotes.Task", StringComparison.Ordinal))
                paragraph.Tag = null;
            return;
        }
        paragraph.Tag = GetIsChecked(paragraph) ? "PaperNotes.Task:1" : "PaperNotes.Task:0";
    }
    public static void ToggleTask(Paragraph paragraph)
    {
        bool next = !GetIsTask(paragraph);
        SetIsTask(paragraph, next);
        if (next) SetIsChecked(paragraph, false);
    }
    public static void EnsureMarker(Paragraph paragraph)
    {
        if (FindMarker(paragraph) != null) return;
        var marker = new TaskMarker();
        marker.SetChecked(GetIsChecked(paragraph));
        marker.ToggleRequested += () =>
        {
            bool previous = GetIsChecked(paragraph);
            SetIsChecked(paragraph, !previous);
            if (HostEditor(paragraph) is NoteEditor editor)
                editor.RecordStructuralUndo(() => SetIsChecked(paragraph, previous));
        };
        // Center within the first line box only — InlineUIContainer does not span wrapped lines,
        // so the circle stays on line 1 as the paragraph grows.
        var host = new InlineUIContainer(marker)
        {
            BaselineAlignment = BaselineAlignment.Center,
            Tag = "TaskMarker"
        };
        if (paragraph.Inlines.FirstInline == null) paragraph.Inlines.Add(host);
        else paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, host);
        if (BodyIsEmpty(paragraph)) paragraph.Inlines.Add(new Run(""));
    }
    static RichTextBox? HostEditor(Paragraph paragraph)
    {
        if (paragraph.Parent is FlowDocument { Parent: RichTextBox rtb }) return rtb;
        DependencyObject? node = paragraph;
        while (node != null)
        {
            if (node is RichTextBox box) return box;
            node = node is FrameworkContentElement fce ? fce.Parent : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }
    public static void RemoveMarker(Paragraph paragraph)
    {
        foreach (var inline in paragraph.Inlines.OfType<Inline>().ToList())
            if (inline is InlineUIContainer { Tag: "TaskMarker" }) paragraph.Inlines.Remove(inline);
    }
    public static TaskMarker? FindMarker(Paragraph paragraph)
    {
        foreach (var inline in paragraph.Inlines)
            if (inline is InlineUIContainer { Child: TaskMarker marker, Tag: "TaskMarker" }) return marker;
        return null;
    }
    public static bool BodyIsEmpty(Paragraph paragraph)
    {
        foreach (var inline in paragraph.Inlines)
        {
            if (inline is InlineUIContainer { Tag: "TaskMarker" }) continue;
            if (inline is LineBreak) continue;
            string text = new TextRange(inline.ContentStart, inline.ContentEnd).Text;
            if (!string.IsNullOrWhiteSpace(text.Replace("\u00A0", ""))) return false;
        }
        return true;
    }
    static void ApplyLayout(Paragraph paragraph, bool task)
    {
        if (task)
        {
            // Hanging indent: first line starts at 0 (marker + body), wrapped lines at Slot (body only).
            paragraph.Margin = new Thickness(TaskMarker.Slot, 0, 0, 0);
            paragraph.TextIndent = -TaskMarker.Slot;
            // Fixed line box so BaselineAlignment.Center is stable regardless of wrap count / glyph metrics.
            paragraph.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
            paragraph.LineHeight = TaskMarker.LineBoxHeight;
        }
        else
        {
            paragraph.ClearValue(Block.MarginProperty);
            paragraph.ClearValue(Paragraph.TextIndentProperty);
            paragraph.ClearValue(Paragraph.LineHeightProperty);
            paragraph.ClearValue(Paragraph.LineStackingStrategyProperty);
            paragraph.Margin = new Thickness(0);
            paragraph.TextIndent = 0;
        }
    }
    /// <summary>Strip all character formatting from task body so Marker never fights bold/highlight/size.</summary>
    public static void ClearBodyCharacterFormatting(Paragraph paragraph)
    {
        var start = ContentStartAfterMarker(paragraph) ?? paragraph.ContentStart;
        var end = paragraph.ContentEnd;
        if (start.CompareTo(end) < 0)
        {
            var range = new TextRange(start, end);
            range.ClearAllProperties();
        }
        paragraph.ClearValue(TextElement.FontWeightProperty);
        paragraph.ClearValue(TextElement.FontStyleProperty);
        paragraph.ClearValue(TextElement.FontSizeProperty);
        paragraph.ClearValue(TextElement.ForegroundProperty);
        paragraph.ClearValue(TextElement.BackgroundProperty);
        paragraph.ClearValue(Inline.TextDecorationsProperty);
    }
    /// <summary>Default task typography; re-apply completion chrome if checked.</summary>
    public static void NormalizeBodyFormatting(Paragraph paragraph)
    {
        ClearBodyCharacterFormatting(paragraph);
        if (GetIsTask(paragraph)) ApplyCheckedLook(paragraph);
    }
    static void ApplyCheckedLook(Paragraph paragraph)
    {
        var start = ContentStartAfterMarker(paragraph);
        if (start == null) return;
        var end = paragraph.ContentEnd;
        if (start.CompareTo(end) >= 0) return;
        var range = new TextRange(start, end);
        bool done = GetIsTask(paragraph) && GetIsChecked(paragraph);
        var current = range.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        range.ApplyPropertyValue(Inline.TextDecorationsProperty, MergeStrike(current, done));
        if (done)
            range.ApplyPropertyValue(TextElement.ForegroundProperty, CreateTaskDoneBrush());
        else
            ClearTaskDoneForeground(paragraph);
    }
    static SolidColorBrush CreateTaskDoneBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x90));
        brush.Freeze();
        return brush;
    }
    static bool IsTaskDoneBrush(Brush? brush) =>
        brush is SolidColorBrush solid && solid.Color.R == 0x9A && solid.Color.G == 0x9A && solid.Color.B == 0x90;
    static void ClearTaskDoneForeground(Paragraph paragraph)
    {
        foreach (var inline in paragraph.Inlines.ToList())
            ClearTaskDoneForegroundInline(inline);
    }
    static void ClearTaskDoneForegroundInline(Inline inline)
    {
        if (inline is InlineUIContainer { Tag: "TaskMarker" }) return;
        if (inline is Span span)
        {
            foreach (var child in span.Inlines.ToList()) ClearTaskDoneForegroundInline(child);
            return;
        }
        object local = inline.ReadLocalValue(TextElement.ForegroundProperty);
        if (local is Brush brush && IsTaskDoneBrush(brush))
            inline.ClearValue(TextElement.ForegroundProperty);
    }
    static object? MergeStrike(TextDecorationCollection? current, bool strike)
    {
        var result = new TextDecorationCollection();
        if (current != null)
            foreach (var d in current)
                if (d.Location != TextDecorationLocation.Strikethrough) result.Add(d);
        if (strike) foreach (var d in TextDecorations.Strikethrough) result.Add(d);
        if (result.Count == 0) return null;
        result.Freeze();
        return result;
    }
    public static TextPointer? ContentStartAfterMarker(Paragraph paragraph)
    {
        foreach (var inline in paragraph.Inlines)
        {
            if (inline is InlineUIContainer { Tag: "TaskMarker" })
                return inline.ElementEnd.GetInsertionPosition(LogicalDirection.Forward);
        }
        return paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
    }
    public static Paragraph? ParagraphAt(TextPointer? pointer)
    {
        DependencyObject? node = pointer?.Parent as DependencyObject;
        while (node != null)
        {
            if (node is Paragraph paragraph) return paragraph;
            node = node is FrameworkContentElement fce ? fce.Parent : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }
    /// <summary>Re-bind markers after XAML load (InlineUIContainer may deserialize without handlers).</summary>
    public static void Rehydrate(FlowDocument document)
    {
        bool previous = SuppressNotify;
        SuppressNotify = true;
        try
        {
            // Snapshot first — marker/layout updates can invalidate a live Blocks enumerator.
            foreach (var block in document.Blocks.OfType<Paragraph>().ToList())
            {
                bool task = GetIsTask(block);
                bool done = GetIsChecked(block);
                if (block.Tag is string tag && tag.StartsWith("PaperNotes.Task", StringComparison.Ordinal))
                {
                    task = true;
                    done = tag.EndsWith(":1", StringComparison.Ordinal);
                }
                if (!task)
                {
                    // Never resurrect chrome from a stale Tag when IsTask is false after edits.
                    RemoveMarker(block);
                    if (block.Tag is string leftover && leftover.StartsWith("PaperNotes.Task", StringComparison.Ordinal))
                        block.Tag = null;
                    continue;
                }
                RemoveMarker(block);
                SetIsTask(block, true);
                SetIsChecked(block, done);
                ClearBodyCharacterFormatting(block);
                EnsureMarker(block);
                ApplyLayout(block, true);
                ApplyCheckedLook(block);
                SyncTag(block);
            }
        }
        finally { SuppressNotify = previous; }
    }
    public static void RemoveOrphanTaskMarkers(FlowDocument document)
    {
        foreach (var block in document.Blocks.OfType<Paragraph>().ToList())
        {
            if (GetIsTask(block)) continue;
            RemoveMarker(block);
            if (block.Tag is string tag && tag.StartsWith("PaperNotes.Task", StringComparison.Ordinal))
                block.Tag = null;
            ApplyLayout(block, false);
        }
    }
    public static void FixTasksAfterUndo(FlowDocument document)
    {
        bool previous = SuppressNotify;
        SuppressNotify = true;
        try
        {
            foreach (var block in document.Blocks.OfType<Paragraph>().ToList())
            {
                bool hasMarker = FindMarker(block) != null;
                bool isTask = GetIsTask(block);
                if (isTask && !hasMarker)
                {
                    block.SetValue(IsTaskProperty, false);
                    block.SetValue(IsCheckedProperty, false);
                    if (block.Tag is string tag && tag.StartsWith("PaperNotes.Task", StringComparison.Ordinal))
                        block.Tag = null;
                    ApplyLayout(block, false);
                }
                else if (!isTask && hasMarker)
                {
                    block.SetValue(IsTaskProperty, true);
                    ApplyLayout(block, true);
                    SyncTag(block);
                }
            }
        }
        finally { SuppressNotify = previous; }
    }
}

/// <summary>Fixed-size checklist circle centered on the first line box (BaselineAlignment).</summary>
public sealed class TaskMarker : Border
{
    public const double Size = 18;
    public const double Gap = 6;
    public const double Slot = Size + Gap; // 24 — hanging indent
    /// <summary>Stable first-line box height (DIP) matching default 16px body + marker.</summary>
    public const double LineBoxHeight = 22;

    public event Action? ToggleRequested;
    private readonly Ellipse ring;
    private readonly TextBlock check;
    public TaskMarker()
    {
        Width = Size;
        Height = Size;
        Margin = new Thickness(0, 0, Gap, 0);
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        Background = Brushes.Transparent;
        BorderThickness = new Thickness(0);
        Cursor = Cursors.Hand;
        Focusable = false;
        var grid = new Grid
        {
            Width = Size,
            Height = Size,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        ring = new Ellipse
        {
            Width = 15,
            Height = 15,
            StrokeThickness = 1.4,
            Stroke = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x63)),
            Fill = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        check = new TextBlock
        {
            Text = "✓",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Symbol"),
            Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x7A, 0x45)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        grid.Children.Add(ring);
        grid.Children.Add(check);
        Child = grid;
        MouseLeftButtonDown += (_, e) => { ToggleRequested?.Invoke(); e.Handled = true; };
    }
    public void SetChecked(bool value)
    {
        check.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        ring.Fill = value ? new SolidColorBrush(Color.FromArgb(0x22, 0x5A, 0x7A, 0x45)) : Brushes.Transparent;
    }
}
