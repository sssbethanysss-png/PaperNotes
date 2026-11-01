using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace PaperNotes;

public sealed class NoteEditor : RichTextBox
{
    public static readonly double[] FontSizes = { 13, 16, 20, 26 };
    public static readonly Color HighlightColor = Color.FromRgb(0xFF, 0xF5, 0x9D);
    private static readonly SolidColorBrush HighlightBrush = CreateFrozenBrush(HighlightColor);
    public string? LoadWarning { get; private set; }
    private Dictionary<DependencyProperty, object>? typingStyle;
    private string typingText = "";
    private int typingOffset;
    private bool composing;
    private int? savedSelStart;
    private int? savedSelEnd;
    private readonly Stack<Action> structuralUndo = new();
    private bool structuralOp;
    private bool preferStructuralUndo;
    public string Text
    {
        get
        {
            string text = new TextRange(Document.ContentStart, Document.ContentEnd).Text;
            // FlowDocument adds one terminating paragraph delimiter. Keep user-created trailing blank lines.
            if (text.EndsWith("\r\n", StringComparison.Ordinal)) text = text[..^2];
            return Normalize(text);
        }
        set { typingStyle = null; Document = PlainDocument(value); }
    }
    public static string Normalize(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color); brush.Freeze(); return brush;
    }
    public NoteEditor(Note note)
    {
        FontSize = 16; AcceptsTab = true; IsUndoEnabled = true; UndoLimit = 100;
        IsDocumentEnabled = true; // Needed so TaskMarker (InlineUIContainer) can receive clicks.
        Document = PlainDocument(note.Content);
        if (!string.IsNullOrEmpty(note.RichTextRtfBase64))
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(note.RichTextRtfBase64);
                if (LooksLikeXaml(bytes))
                {
                    try
                    {
                        // Keep attached task DPs in XAML; SuppressNotify prevents EnsureMarker during Parse.
                        string xamlText = Encoding.UTF8.GetString(bytes);
                        FlowDocument? loaded;
                        TaskParagraph.SuppressNotify = true;
                        try
                        {
                            loaded = XamlReader.Parse(xamlText) as FlowDocument;
                            if (loaded is null) throw new InvalidDataException("not a FlowDocument");
                            // Rehydrate before attaching to RichTextBox so marker inserts don't race the editor.
                            TaskParagraph.Rehydrate(loaded);
                        }
                        finally { TaskParagraph.SuppressNotify = false; }
                        Document = loaded;
                        if (!SameDocumentText(Text, note.Content))
                        {
                            Document = PlainDocument(note.Content);
                            LoadWarning = Loc.T("Format.MismatchPlainTextKept");
                        }
                        else if (Normalize(Text) != Normalize(note.Content))
                            LoadWarning = Loc.T("Format.MinorMismatchKept");
                    }
                    catch (Exception ex) when (ex is XamlParseException or InvalidOperationException or InvalidDataException or ArgumentException)
                    {
                        string parseError = ex.GetType().Name + ": " + ex.Message;
                        Document = PlainDocument(note.Content);
                        try
                        {
                            string sanitized = SanitizeTaskXamlForLoad(Encoding.UTF8.GetString(bytes));
                            FlowDocument? fallbackDoc;
                            TaskParagraph.SuppressNotify = true;
                            try
                            {
                                fallbackDoc = XamlReader.Parse(sanitized) as FlowDocument;
                                if (fallbackDoc != null) TaskParagraph.Rehydrate(fallbackDoc);
                            }
                            finally { TaskParagraph.SuppressNotify = false; }
                            if (fallbackDoc != null)
                            {
                                Document = fallbackDoc;
                                LoadWarning = Document.Blocks.OfType<Paragraph>().Any(p => TaskParagraph.GetIsTask(p) || p.Tag is string s && s.StartsWith("PaperNotes.Task")) ? null : parseError;
                            }
                            else
                            {
                                using var rangeStream = new MemoryStream(Encoding.UTF8.GetBytes(sanitized));
                                new TextRange(Document.ContentStart, Document.ContentEnd).Load(rangeStream, DataFormats.Xaml);
                                TaskParagraph.Rehydrate(Document);
                                LoadWarning = Document.Blocks.OfType<Paragraph>().Any(p => TaskParagraph.GetIsTask(p) || p.Tag is string s && s.StartsWith("PaperNotes.Task")) ? null : parseError;
                            }
                        }
                        catch (Exception ex2)
                        {
                            Document = PlainDocument(note.Content);
                            LoadWarning = parseError + " | " + ex2.Message;
                        }
                    }
                }
                else
                {
                    using var rtfStream = new MemoryStream(bytes);
                    new TextRange(Document.ContentStart, Document.ContentEnd).Load(rtfStream, DataFormats.Rtf);
                    if (Text != Normalize(note.Content)) throw new InvalidDataException(Loc.T("Format.TextMismatch"));
                }
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or IOException or InvalidDataException)
            {
                Document = PlainDocument(note.Content);
                LoadWarning = Loc.T("Format.RestoreFailed");
            }
        }
        Document.PagePadding = new Thickness(0);
        var paragraphStyle = new Style(typeof(Paragraph)); paragraphStyle.Setters.Add(new Setter(Paragraph.MarginProperty, new Thickness(0)));
        Resources[typeof(Paragraph)] = paragraphStyle;
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/PaperNotes;component/NoteScroll.xaml", UriKind.Absolute)
        });
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        TaskParagraph.Changed += OnChecklistChanged;
        Unloaded += (_, _) => TaskParagraph.Changed -= OnChecklistChanged;
        // Discard loading operations from undo history, preserving ordinary editing undo/redo afterwards.
        IsUndoEnabled = false; IsUndoEnabled = true;
        CommandManager.AddPreviewExecutedHandler(this, OnPreviewFormatCommand);
        CommandManager.AddPreviewExecutedHandler(this, OnPreviewUndoCommand);
        DataObject.AddPastingHandler(this, OnPasteIntoEditor);
        TextChanged += OnEditorTextChanged;
        PreviewMouseDown += (_, _) => typingStyle = null;
        SelectionChanged += (_, _) =>
        {
            if (typingStyle != null && !composing && Text == typingText
                && (!Selection.IsEmpty || Normalize(new TextRange(Document.ContentStart, Selection.Start).Text).Length != typingOffset)) typingStyle = null;
        };
        PreviewKeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Escape) typingStyle = null; };
        PreviewKeyDown += OnTaskKeys;
        PreviewKeyDown += OnPlainPasteShortcut;
        TextCompositionManager.AddPreviewTextInputStartHandler(this, (_, _) => composing = true);
        AddHandler(TextCompositionManager.TextInputEvent, new TextCompositionEventHandler((_, _) =>
        {
            composing = false;
            // Run after the native editor commits input; never mutate a live IME composition.
            if (typingStyle != null) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(ApplyCommittedTypingStyle));
        }), true);
    }
    void OnPreviewUndoCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command != ApplicationCommands.Undo) return;
        if (preferStructuralUndo && structuralUndo.Count > 0)
        {
            e.Handled = true;
            PopStructuralUndo();
        }
    }
    public bool HasUndo => CanUndo || structuralUndo.Count > 0;
    public void PerformUndo()
    {
        if (preferStructuralUndo && structuralUndo.Count > 0)
        {
            PopStructuralUndo();
            return;
        }
        if (CanUndo)
        {
            Undo();
            TaskParagraph.FixTasksAfterUndo(Document);
            preferStructuralUndo = structuralUndo.Count > 0;
            return;
        }
        if (structuralUndo.Count > 0) PopStructuralUndo();
    }
    void PopStructuralUndo()
    {
        structuralOp = true;
        try { structuralUndo.Pop()(); }
        finally
        {
            structuralOp = false;
            preferStructuralUndo = structuralUndo.Count > 0;
        }
    }
    public void RecordStructuralUndo(Action undo) => PushStructuralUndo(undo);
    void PushStructuralUndo(Action undo)
    {
        structuralUndo.Push(undo);
        preferStructuralUndo = true;
        if (structuralUndo.Count <= UndoLimit) return;
        var newest = structuralUndo.ToArray(); // index 0 = newest
        structuralUndo.Clear();
        for (int i = Math.Min(UndoLimit, newest.Length) - 1; i >= 0; i--)
            structuralUndo.Push(newest[i]);
    }
    void OnPreviewFormatCommand(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == EditingCommands.ToggleItalic) { e.Handled = true; return; } // Italic removed from product surface.
        if (IsCharacterFormatCommand(e.Command) && SelectionTouchesTask())
        {
            typingStyle = null;
            e.Handled = true;
            return;
        }
        if (!Selection.IsEmpty) { typingStyle = null; return; }
        if (e.Command == EditingCommands.ToggleBold)
            ApplyTypingProperty(TextElement.FontWeightProperty, Equals(CurrentProperty(TextElement.FontWeightProperty), FontWeights.Bold) ? FontWeights.Normal : FontWeights.Bold);
        else if (e.Command == EditingCommands.ToggleUnderline)
            ApplyTypingProperty(Inline.TextDecorationsProperty, CurrentProperty(Inline.TextDecorationsProperty) is TextDecorationCollection lines && lines.Count > 0 ? new TextDecorationCollection() : TextDecorations.Underline);
        else if (e.Command == EditingCommands.IncreaseFontSize)
            ChangeFontSize(1);
        else if (e.Command == EditingCommands.DecreaseFontSize)
            ChangeFontSize(-1);
        else return;
        e.Handled = true;
    }
    static bool IsCharacterFormatCommand(ICommand command) =>
        command == EditingCommands.ToggleBold
        || command == EditingCommands.ToggleUnderline
        || command == EditingCommands.ToggleItalic
        || command == EditingCommands.IncreaseFontSize
        || command == EditingCommands.DecreaseFontSize
        || command == EditingCommands.ToggleSubscript
        || command == EditingCommands.ToggleSuperscript;
    void OnPlainPasteShortcut(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V) return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != (ModifierKeys.Control | ModifierKeys.Shift)) return;
        e.Handled = true;
        InsertPlainText(Clipboard.ContainsText(TextDataFormat.UnicodeText)
            ? Clipboard.GetText(TextDataFormat.UnicodeText)
            : "");
    }
    public void InsertPlainText(string text)
    {
        if (text.Length == 0 && Selection.IsEmpty) return;
        Selection.Text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", Environment.NewLine);
        if (SelectionTouchesTask())
        {
            foreach (var paragraph in Document.Blocks.OfType<Paragraph>().Where(TaskParagraph.GetIsTask).ToList())
                TaskParagraph.NormalizeBodyFormatting(paragraph);
        }
    }
    void OnPasteIntoEditor(object sender, DataObjectPastingEventArgs e)
    {
        bool forcePlain = SelectionTouchesTask();
        if (!TryPreparePaste(e.DataObject, forcePlain, out DataObject prepared, out string format, out _))
        {
            e.CancelCommand();
            return;
        }
        e.DataObject = prepared;
        e.FormatToApply = format;
        if (forcePlain)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
            {
                foreach (var paragraph in Document.Blocks.OfType<Paragraph>().Where(TaskParagraph.GetIsTask).ToList())
                    TaskParagraph.NormalizeBodyFormatting(paragraph);
            }));
        }
    }
    /// <summary>Build a paste payload. Prefer sanitized rich text; fall back to Unicode when it differs.</summary>
    public static bool TryPreparePaste(IDataObject source, bool forcePlain, out DataObject prepared, out string format, out string mode)
    {
        prepared = new DataObject();
        format = DataFormats.UnicodeText;
        mode = "reject";
        if (forcePlain)
        {
            if (!TryCopyUnicodeText(source, out string plain)) return false;
            prepared.SetData(DataFormats.UnicodeText, plain);
            format = DataFormats.UnicodeText;
            mode = "plain-forced";
            return true;
        }
        if (TryBuildSanitizedRichPaste(source, out prepared, out format, out mode)) return true;
        if (!TryCopyUnicodeText(source, out string unicode)) return false;
        prepared = new DataObject();
        prepared.SetData(DataFormats.UnicodeText, unicode);
        format = DataFormats.UnicodeText;
        mode = mode == "rich-mismatch" ? "plain-fallback-mismatch" : "plain-fallback";
        return true;
    }
    static bool TryCopyUnicodeText(IDataObject source, out string text)
    {
        text = "";
        // Never use ANSI DataFormats.Text — Word CJK becomes mojibake.
        if (!source.GetDataPresent(DataFormats.UnicodeText)) return false;
        if (source.GetData(DataFormats.UnicodeText) is not string value) return false;
        text = value;
        return true;
    }
    static bool TryBuildSanitizedRichPaste(IDataObject source, out DataObject prepared, out string format, out string mode)
    {
        prepared = new DataObject();
        format = DataFormats.Xaml;
        mode = "rich-unavailable";
        try
        {
            FlowDocument? document = null;
            if (source.GetDataPresent(DataFormats.Rtf)
                && source.GetData(DataFormats.Rtf) is string rtf && rtf.Length > 0)
            {
                document = new FlowDocument();
                // Clipboard RTF is an 8-bit stream stuffed into a BSTR; map each char to a byte.
                using var stream = new MemoryStream(RtfStringToBytes(rtf));
                new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Rtf);
                mode = "rich-rtf";
            }
            else if (source.GetDataPresent(DataFormats.Xaml)
                && source.GetData(DataFormats.Xaml) is string xaml && xaml.Length > 0)
            {
                document = new FlowDocument();
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
                new TextRange(document.ContentStart, document.ContentEnd).Load(stream, DataFormats.Xaml);
                mode = "rich-xaml";
            }
            if (document == null) return false;
            SanitizePastedDocument(document);
            string richPlain = Normalize(new TextRange(document.ContentStart, document.ContentEnd).Text);
            if (TryCopyUnicodeText(source, out string unicodeTruth)
                && !PlainTextMatches(richPlain, unicodeTruth))
            {
                // Word RTF parsing can corrupt characters. Fall back to Unicode text if they differ.
                mode = "rich-mismatch";
                return false;
            }
            using var output = new MemoryStream();
            new TextRange(document.ContentStart, document.ContentEnd).Save(output, DataFormats.Xaml);
            string sanitized = Encoding.UTF8.GetString(output.ToArray());
            prepared = new DataObject();
            prepared.SetData(DataFormats.Xaml, sanitized);
            prepared.SetData(DataFormats.UnicodeText, richPlain);
            format = DataFormats.Xaml;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or XamlParseException)
        {
            mode = "rich-error";
            return false;
        }
    }
    public static bool PlainTextMatches(string a, string b) =>
        Normalize(a).TrimEnd('\n') == Normalize(b).TrimEnd('\n');
    static byte[] RtfStringToBytes(string rtf)
    {
        var bytes = new byte[rtf.Length];
        for (int i = 0; i < rtf.Length; i++) bytes[i] = (byte)rtf[i];
        return bytes;
    }
    /// <summary>Remove Word layout that breaks FlowDocument wrapping; snap font sizes to the app ladder.</summary>
    public static void SanitizePastedDocument(FlowDocument document)
    {
        document.PagePadding = new Thickness(0);
        document.TextAlignment = TextAlignment.Left;
        // Word RTF may set a fixed page width that disables wrapping and clips CJK at the window edge.
        document.ClearValue(FlowDocument.PageWidthProperty);
        document.ClearValue(FlowDocument.PageHeightProperty);
        document.ClearValue(TextElement.FontFamilyProperty);
        foreach (var block in document.Blocks.ToList())
        {
            if (block is not Paragraph paragraph)
            {
                // Word tables/sections: keep text only.
                string text = new TextRange(block.ContentStart, block.ContentEnd).Text;
                var replacement = new Paragraph(new Run(Normalize(text).Replace("\n", ""))) { Margin = new Thickness(0) };
                document.Blocks.InsertBefore(block, replacement);
                document.Blocks.Remove(block);
                SanitizeInlines(replacement);
                continue;
            }
            paragraph.Margin = new Thickness(0);
            paragraph.Padding = new Thickness(0);
            paragraph.TextIndent = 0;
            paragraph.ClearValue(Paragraph.LineHeightProperty);
            paragraph.ClearValue(Block.LineStackingStrategyProperty);
            paragraph.ClearValue(Block.TextAlignmentProperty);
            paragraph.ClearValue(Paragraph.FlowDirectionProperty);
            paragraph.ClearValue(TextElement.FontFamilyProperty);
            object paragraphSize = paragraph.ReadLocalValue(TextElement.FontSizeProperty);
            if (paragraphSize is double ps && !double.IsNaN(ps) && ps > 0)
                paragraph.SetValue(TextElement.FontSizeProperty, NearestFontSize(ps));
            SanitizeInlines(paragraph);
        }
    }
    static void SanitizeInlines(Paragraph paragraph)
    {
        foreach (var inline in paragraph.Inlines.ToList())
            SanitizeInline(inline);
    }
    static void SanitizeInline(Inline inline)
    {
        if (inline is InlineUIContainer or Floater or Figure)
        {
            string text = new TextRange(inline.ContentStart, inline.ContentEnd).Text;
            var run = new Run(text);
            inline.SiblingInlines.InsertBefore(inline, run);
            inline.SiblingInlines.Remove(inline);
            SnapInlineFontSize(run);
            run.ClearValue(TextElement.FontFamilyProperty);
            return;
        }
        if (inline is Span span)
        {
            foreach (var child in span.Inlines.ToList()) SanitizeInline(child);
        }
        inline.ClearValue(FrameworkContentElement.CursorProperty);
        inline.ClearValue(TextElement.FontFamilyProperty);
        SnapInlineFontSize(inline);
    }
    /// <summary>Apply a prepared paste payload without reading the system clipboard (tests / programmatic).</summary>
    public void ApplyPreparedPaste(DataObject data, string format)
    {
        if (format == DataFormats.Xaml && data.GetData(DataFormats.Xaml) is string xaml)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
            Selection.Load(stream, DataFormats.Xaml);
            return;
        }
        if (data.GetData(DataFormats.UnicodeText) is string text)
            InsertPlainText(text);
    }
    static void SnapInlineFontSize(Inline inline)
    {
        object local = inline.ReadLocalValue(TextElement.FontSizeProperty);
        if (local is double size && !double.IsNaN(size) && size > 0)
            inline.SetValue(TextElement.FontSizeProperty, NearestFontSize(size));
    }
    public static double NearestFontSize(double size)
    {
        double best = FontSizes[0];
        double bestDist = Math.Abs(best - size);
        foreach (double candidate in FontSizes)
        {
            double dist = Math.Abs(candidate - size);
            if (dist < bestDist) { best = candidate; bestDist = dist; }
        }
        return best;
    }
    void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!structuralOp) preferStructuralUndo = false;
        if (TaskParagraph.SuppressNotify || scrubbingTasks) return;
        scrubbingTasks = true;
        try { TaskParagraph.RemoveOrphanTaskMarkers(Document); }
        finally { scrubbingTasks = false; }
    }
    bool scrubbingTasks;
    private object CurrentProperty(DependencyProperty property) => Selection.IsEmpty && typingStyle?.TryGetValue(property, out var value) == true ? value : Selection.GetPropertyValue(property);
    private void ApplyTypingProperty(DependencyProperty property, object value)
    {
        if (SelectionTouchesTask()) { typingStyle = null; return; }
        if (typingStyle == null)
        {
            typingText = Text;
            typingOffset = Normalize(new TextRange(Document.ContentStart, Selection.Start).Text).Length;
            typingStyle = new Dictionary<DependencyProperty, object>();
            foreach (var p in new[] { TextElement.FontWeightProperty, TextElement.FontStyleProperty, TextElement.FontSizeProperty, Inline.TextDecorationsProperty, TextElement.BackgroundProperty })
            {
                var current = Selection.GetPropertyValue(p);
                if (current != DependencyProperty.UnsetValue && current != null) typingStyle[p] = current;
            }
        }
        typingStyle[property] = value;
    }
    public void ApplyCommittedTypingStyle()
    {
        if (typingStyle == null || composing) return;
        if (SelectionTouchesTask()) { typingStyle = null; return; }
        var text = Text; int added = text.Length - typingText.Length;
        if (added <= 0) return;
        if (typingOffset > typingText.Length || !text.StartsWith(typingText[..typingOffset], StringComparison.Ordinal)
            || !text.EndsWith(typingText[typingOffset..], StringComparison.Ordinal)) { typingStyle = null; return; }
        var style = typingStyle; typingStyle = null;
        var range = new TextRange(PositionAtTextOffset(typingOffset), PositionAtTextOffset(typingOffset + added));
        BeginChange();
        try
        {
            foreach (var pair in style) range.ApplyPropertyValue(pair.Key, pair.Value);
            range.ApplyPropertyValue(FrameworkElement.LanguageProperty, System.Windows.Markup.XmlLanguage.GetLanguage(InputLanguageManager.Current.CurrentInputLanguage.IetfLanguageTag));
            CaretPosition = range.End.GetInsertionPosition(LogicalDirection.Backward);
        }
        finally { EndChange(); }
    }
    public void RememberSelection()
    {
        savedSelStart = Normalize(new TextRange(Document.ContentStart, Selection.Start).Text).Length;
        savedSelEnd = Normalize(new TextRange(Document.ContentStart, Selection.End).Text).Length;
    }
    public void RestoreSelection(bool takeFocus = true)
    {
        if (savedSelStart is not int start || savedSelEnd is not int end) return;
        if (takeFocus) Focus();
        try
        {
            var a = PositionAtTextOffset(Math.Min(start, end));
            var b = PositionAtTextOffset(Math.Max(start, end));
            Selection.Select(a, b);
        }
        // Saved offsets can become invalid after a format change; leave the current selection.
        catch (ArgumentException) { }
    }
    public void RunKeepingSelection(Action action)
    {
        RememberSelection();
        // Restore the remembered range even if focus is on the format popup.
        RestoreSelection(takeFocus: false);
        action();
        RememberSelection();
        // Refocus so selection paint returns; StaysOpen keeps the popup open.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => RestoreSelection(takeFocus: true)));
    }
    public bool IsHighlightActive()
    {
        var value = CurrentProperty(TextElement.BackgroundProperty);
        if (value == null || value == DependencyProperty.UnsetValue) return false;
        if (value is not SolidColorBrush brush || brush.Color.A == 0) return false;
        return ColorsEqual(brush.Color, HighlightColor)
            || (brush.Color.R >= 0xE8 && brush.Color.G >= 0xD0 && brush.Color.B <= 0xC0); // RTF may remap yellows
    }
    public void SelectCharacterRange(int start, int length)
    {
        int end = Math.Max(start, start + length);
        Selection.Select(PositionAtTextOffset(Math.Max(0, start)), PositionAtTextOffset(end));
    }
    public void ToggleHighlight()
    {
        if (SelectionTouchesTask()) return;
        bool on = IsHighlightActive();
        if (Selection.IsEmpty)
            ApplyTypingProperty(TextElement.BackgroundProperty, on ? Brushes.Transparent : HighlightBrush);
        else
        {
            typingStyle = null;
            Selection.ApplyPropertyValue(TextElement.BackgroundProperty, on ? Brushes.Transparent : HighlightBrush);
        }
    }
    private static bool ColorsEqual(Color a, Color b) => a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;
    private TextPointer PositionAtTextOffset(int offset)
    {
        int low = 0, high = Document.ContentStart.GetOffsetToPosition(Document.ContentEnd);
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            var p = Document.ContentStart.GetPositionAtOffset(mid)!;
            if (Normalize(new TextRange(Document.ContentStart, p).Text).Length < offset) low = mid + 1; else high = mid;
        }
        return Document.ContentStart.GetPositionAtOffset(low)!.GetInsertionPosition(LogicalDirection.Forward);
    }
    private static FlowDocument PlainDocument(string text)
    {
        var document = new FlowDocument { PagePadding = new Thickness(0) };
        foreach (string line in Normalize(text).Split('\n')) document.Blocks.Add(new Paragraph(new Run(line)) { Margin = new Thickness(0) });
        return document;
    }
    private static bool LooksLikeXaml(byte[] bytes)
    {
        int i = 0;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) i = 3;
        while (i < bytes.Length && bytes[i] <= 0x20) i++;
        return i < bytes.Length && bytes[i] == (byte)'<';
    }
    /// <summary>True when rich text matches plain Content aside from checklist marker whitespace.</summary>
    private static bool SameDocumentText(string loaded, string plain)
    {
        string a = Regex.Replace(Normalize(loaded).TrimEnd('\n'), @"[ \t]+", " ").Trim();
        string b = Regex.Replace(Normalize(plain).TrimEnd('\n'), @"[ \t]+", " ").Trim();
        return a == b;
    }
    /// <summary>
    /// Strip attached task DPs before fallback XamlReader.Parse — setting them during parse can mutate Inlines.
    /// Tag (PaperNotes.Task:0/1) is enough for Rehydrate.
    /// </summary>
    private static string SanitizeTaskXamlForLoad(string xaml)
    {
        xaml = Regex.Replace(xaml, @"\s*pn:TaskParagraph\.IsTask=""[^""]*""", "", RegexOptions.CultureInvariant);
        xaml = Regex.Replace(xaml, @"\s*pn:TaskParagraph\.IsChecked=""[^""]*""", "", RegexOptions.CultureInvariant);
        return xaml;
    }
    /// <summary>Serialize for Note.RichTextRtfBase64. Name is historical; payload is Base64 XAML.</summary>
    public string SaveRtf()
    {
        // Snapshot without mutating the live Document so autosave does not clear Undo.
        // Strip InlineUIContainer markers; Tag / IsTask carry checklist state for Rehydrate.
        string xaml = XamlWriter.Save(Document);
        xaml = StripLiveTaskChromeFromXaml(xaml);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(xaml));
    }
    static string StripLiveTaskChromeFromXaml(string xaml)
    {
        xaml = Regex.Replace(xaml, @"<InlineUIContainer\b[^>]*/>", "", RegexOptions.IgnoreCase);
        xaml = Regex.Replace(xaml, @"<InlineUIContainer\b[^>]*>[\s\S]*?</InlineUIContainer>", "", RegexOptions.IgnoreCase);
        return xaml;
    }
    public void ChangeFontSize(int direction)
    {
        if (SelectionTouchesTask()) return;
        double current = ReadCurrentFontSize();
        double next = StepFontSize(current, direction);
        if (Selection.IsEmpty) ApplyTypingProperty(TextElement.FontSizeProperty, next);
        else { typingStyle = null; Selection.ApplyPropertyValue(TextElement.FontSizeProperty, next); }
    }
    double ReadCurrentFontSize()
    {
        object value = CurrentProperty(TextElement.FontSizeProperty);
        if (value is double size && !double.IsNaN(size) && size > 0) return size;
        return FontSize;
    }
    public static double StepFontSize(double current, int direction)
    {
        int nearest = 0;
        for (int i = 1; i < FontSizes.Length; i++)
            if (Math.Abs(FontSizes[i] - current) < Math.Abs(FontSizes[nearest] - current)) nearest = i;
        int idx = nearest;
        if (Math.Abs(FontSizes[nearest] - current) <= 0.1)
            idx = nearest + (direction > 0 ? 1 : -1);
        else if (direction > 0)
            idx = FontSizes[nearest] > current ? nearest : nearest + 1;
        else
            idx = FontSizes[nearest] < current ? nearest : nearest - 1;
        return FontSizes[Math.Clamp(idx, 0, FontSizes.Length - 1)];
    }
    private void OnChecklistChanged()
    {
        if (structuralOp || TaskParagraph.SuppressNotify) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(OnChecklistChanged); return; }
        RaiseEvent(new TextChangedEventArgs(TextChangedEvent, UndoAction.Create));
    }
    public bool SelectionTouchesTask() => TaskParagraphsInPlay().Any(TaskParagraph.GetIsTask);
    public void ToggleTaskAtCaret()
    {
        var paragraphs = TaskParagraphsInPlay();
        if (paragraphs.Count == 0) return;
        bool turnOn = paragraphs.Any(p => !TaskParagraph.GetIsTask(p));
        typingStyle = null;
        var snapshot = paragraphs.Select(p => (Paragraph: p, Task: TaskParagraph.GetIsTask(p), Checked: TaskParagraph.GetIsChecked(p))).ToList();
        structuralOp = true;
        try
        {
            foreach (var paragraph in paragraphs)
            {
                if (turnOn)
                {
                    ClearParagraphCharacterFormatting(paragraph);
                    TaskParagraph.SetIsTask(paragraph, true);
                    TaskParagraph.SetIsChecked(paragraph, false);
                }
                else TaskParagraph.SetIsTask(paragraph, false);
            }
            PushStructuralUndo(() =>
            {
                structuralOp = true;
                try
                {
                    foreach (var item in snapshot)
                    {
                        TaskParagraph.SetIsTask(item.Paragraph, item.Task);
                        TaskParagraph.SetIsChecked(item.Paragraph, item.Checked);
                        if (item.Task) TaskParagraph.EnsureMarker(item.Paragraph);
                        else { TaskParagraph.RemoveMarker(item.Paragraph); TaskParagraph.RemoveOrphanTaskMarkers(Document); }
                    }
                }
                finally { structuralOp = false; }
            });
        }
        finally { structuralOp = false; }
        var focus = paragraphs[^1];
        var after = TaskParagraph.ContentStartAfterMarker(focus);
        if (after != null) CaretPosition = after;
    }
    /// <summary>Clear character formatting on a paragraph; keep plain text and any task marker.</summary>
    static void ClearParagraphCharacterFormatting(Paragraph paragraph)
        => TaskParagraph.ClearBodyCharacterFormatting(paragraph);
    List<Paragraph> TaskParagraphsInPlay()
    {
        TextPointer start = Selection.IsEmpty ? CaretPosition : Selection.Start;
        TextPointer end = Selection.IsEmpty ? CaretPosition : Selection.End;
        if (start == null || end == null)
        {
            var fallback = TaskParagraph.ParagraphAt(CaretPosition);
            return fallback == null ? new List<Paragraph>() : new List<Paragraph> { fallback };
        }
        if (start.CompareTo(end) > 0) (start, end) = (end, start);
        var paragraphs = Document.Blocks.OfType<Paragraph>()
            .Where(p => p.ContentEnd.CompareTo(start) > 0 && p.ContentStart.CompareTo(end) <= 0)
            .ToList();
        if (paragraphs.Count > 0) return paragraphs;
        var caret = TaskParagraph.ParagraphAt(CaretPosition) ?? TaskParagraph.ParagraphAt(Selection.Start);
        return caret == null ? new List<Paragraph>() : new List<Paragraph> { caret };
    }
    private void OnTaskKeys(object sender, KeyEventArgs e)
    {
        var paragraph = TaskParagraph.ParagraphAt(CaretPosition);
        if (paragraph == null || !TaskParagraph.GetIsTask(paragraph)) return;
        if (e.Key == Key.Back && TaskParagraph.BodyIsEmpty(paragraph) && Selection.IsEmpty)
        {
            // Empty task + Backspace: clear task chrome immediately (no deferred rehydrate).
            e.Handled = true;
            bool wasTask = true;
            bool wasChecked = TaskParagraph.GetIsChecked(paragraph);
            structuralOp = true;
            try
            {
                TaskParagraph.SetIsTask(paragraph, false);
                TaskParagraph.RemoveOrphanTaskMarkers(Document);
                PushStructuralUndo(() =>
                {
                    structuralOp = true;
                    try
                    {
                        TaskParagraph.SetIsTask(paragraph, wasTask);
                        TaskParagraph.SetIsChecked(paragraph, wasChecked);
                    }
                    finally { structuralOp = false; }
                });
            }
            finally { structuralOp = false; }
            CaretPosition = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
            return;
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && Selection.IsEmpty)
        {
            e.Handled = true;
            InsertTaskParagraphAfter(paragraph);
        }
    }
    /// <summary>Split at caret and mark the new paragraph as a task.</summary>
    public void InsertTaskParagraphAfter(Paragraph current)
    {
        typingStyle = null;
        var bodyStart = TaskParagraph.ContentStartAfterMarker(current) ?? current.ContentStart;
        var caret = CaretPosition ?? current.ContentEnd;
        if (caret.CompareTo(bodyStart) < 0) caret = bodyStart;
        string moved = "";
        if (caret.CompareTo(current.ContentEnd) < 0)
        {
            var tail = new TextRange(caret, current.ContentEnd);
            moved = tail.Text.Replace("\r", "");
            BeginChange();
            try { tail.Text = ""; }
            finally { EndChange(); }
        }
        var next = new Paragraph { Margin = new Thickness(0) };
        next.Inlines.Add(new Run(moved.TrimEnd('\n')));
        Document.Blocks.InsertAfter(current, next);
        ClearParagraphCharacterFormatting(next);
        TaskParagraph.SetIsTask(next, true);
        TaskParagraph.SetIsChecked(next, false);
        var after = TaskParagraph.ContentStartAfterMarker(next);
        if (after != null) CaretPosition = after;
    }
    public ContextMenu FormatMenu()
    {
        // Do not Focus() here — that dismisses the parent ContextMenu.
        RememberSelection();
        var menu = new ContextMenu();
        void Keep(Action action) => RunKeepingSelection(action);
        bool allowFormat = !SelectionTouchesTask();
        var bold = new MenuItem { Header = Loc.T("Format.Bold"), InputGestureText = "Ctrl+B", StaysOpenOnClick = true, IsEnabled = allowFormat };
        bold.Click += (_, _) => Keep(() => EditingCommands.ToggleBold.Execute(null, this));
        menu.Items.Add(bold);
        var underline = new MenuItem { Header = Loc.T("Format.Underline"), InputGestureText = "Ctrl+U", StaysOpenOnClick = true, IsEnabled = allowFormat };
        underline.Click += (_, _) => Keep(() => EditingCommands.ToggleUnderline.Execute(null, this));
        menu.Items.Add(underline);
        var highlight = new MenuItem { Header = Loc.T("Format.Highlight"), StaysOpenOnClick = true, IsEnabled = allowFormat };
        highlight.Click += (_, _) => Keep(ToggleHighlight);
        menu.Items.Add(highlight);
        var larger = new MenuItem { Header = Loc.T("Format.IncreaseSize"), StaysOpenOnClick = true, IsEnabled = allowFormat }; larger.Click += (_, _) => Keep(() => ChangeFontSize(1)); menu.Items.Add(larger);
        var smaller = new MenuItem { Header = Loc.T("Format.DecreaseSize"), StaysOpenOnClick = true, IsEnabled = allowFormat }; smaller.Click += (_, _) => Keep(() => ChangeFontSize(-1)); menu.Items.Add(smaller);
        menu.Items.Add(new Separator());
        var task = new MenuItem { Header = Loc.T("Format.Checklist"), StaysOpenOnClick = true };
        task.Click += (_, _) => Keep(ToggleTaskAtCaret);
        menu.Items.Add(task);
        return menu;
    }
    public Border BuildFormatPopupContent(Action? afterApply = null)
    {
        var panel = new StackPanel { Margin = new Thickness(4) };
        bool allowFormat = !SelectionTouchesTask();
        Button Chip(string label, Action apply, bool enabled = true)
        {
            var button = new Button
            {
                Content = label,
                Focusable = false,
                IsEnabled = enabled,
                Opacity = enabled ? 1 : 0.38,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(10, 7, 14, 7),
                Margin = new Thickness(0, 1, 0, 1),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0x35, 0x37, 0x2F)),
            };
            button.Click += (_, _) =>
            {
                if (!button.IsEnabled) return;
                RunKeepingSelection(apply);
                afterApply?.Invoke();
            };
            return button;
        }
        panel.Children.Add(Chip(Loc.T("Format.Bold"), () => EditingCommands.ToggleBold.Execute(null, this), allowFormat));
        panel.Children.Add(Chip(Loc.T("Format.Underline"), () => EditingCommands.ToggleUnderline.Execute(null, this), allowFormat));
        panel.Children.Add(Chip(Loc.T("Format.Highlight"), ToggleHighlight, allowFormat));
        panel.Children.Add(Chip(Loc.T("Format.IncreaseSize"), () => ChangeFontSize(1), allowFormat));
        panel.Children.Add(Chip(Loc.T("Format.DecreaseSize"), () => ChangeFontSize(-1), allowFormat));
        panel.Children.Add(new Separator { Margin = new Thickness(6, 4, 6, 4) });
        panel.Children.Add(Chip(Loc.T("Format.Checklist"), ToggleTaskAtCaret, enabled: true));
        return new Border
        {
            Child = panel,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE3, 0xDB)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(2),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 2, Opacity = 0.18, Color = Colors.Black
            }
        };
    }
}
