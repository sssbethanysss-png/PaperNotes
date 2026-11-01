using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PaperNotes;

static class RichTextTests
{
    public static void Run(string root)
    {
        var store = new NoteStore(Path.Combine(root, "rich"));
        var note = new Note { Content = "中文 日本語 English\n第二行\n" }; store.Save(note);
        var window = new NoteWindow(note, store); window.Show(); var editor = window.Editor; editor.Focus();
        SelectFirst(editor, 2);
        Tests.Check(editor.Selection.Text == "中文", "rich selection targets only requested characters");
        EditingCommands.ToggleBold.Execute(null, editor);
        EditingCommands.ToggleUnderline.Execute(null, editor);
        editor.ToggleHighlight();
        editor.ChangeFontSize(1);
        Tests.Check(Equals(editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold)
            && editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection lines && lines.Count > 0
            && editor.IsHighlightActive()
            && Equals(editor.Selection.GetPropertyValue(TextElement.FontSizeProperty), 20d), "bold underline highlight and size apply to selection");
        Tests.Pump(); var saved = store.Load().Single();
        Tests.Check(saved.Content == "中文 日本語 English\n第二行\n" && saved.FormatVersion == 2 && saved.RichTextRtfBase64 != null, "format-only changes autosave rich payload and preserve plain text/trailing newline");
        window.Destroy();
        var restored = new NoteWindow(saved, store); restored.Show(); Tests.Pump(100); restored.Activate(); editor = restored.Editor; editor.Focus();
        Tests.Check(editor.LoadWarning == null && editor.Text == saved.Content, "rich Unicode and trailing lines load losslessly");
        editor.SelectCharacterRange(0, 2);
        var weight = editor.Selection.GetPropertyValue(TextElement.FontWeightProperty);
        var size = editor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
        Tests.Check(Equals(weight, FontWeights.Bold), "bold survives restart");
        Tests.Check(size is double d && Math.Abs(d - 20) < 0.5, "font size survives restart");
        Tests.Check(editor.IsHighlightActive(), "highlight survives restart");
        Tests.Check(Equals(weight, FontWeights.Bold) && size is double sz && Math.Abs(sz - 20) < 0.5 && editor.IsHighlightActive(), "selected rich formatting survives restart");
        EditingCommands.ToggleBold.Execute(null, editor);
        editor.Undo(); Tests.Check(Equals(editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold), "format undo restores bold");
        editor.Redo(); Tests.Check(!Equals(editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold), "format redo reapplies change");
        var caret = editor.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
        editor.Selection.Select(caret, caret); editor.Focus(); Tests.Pump(150);
        EditingCommands.ToggleBold.Execute(null, editor);
        editor.ChangeFontSize(1);
        editor.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, new TextComposition(InputManager.Current, editor, "后续"))
            { RoutedEvent = TextCompositionManager.TextInputEvent }); Tests.Pump(100);
        Tests.Check(editor.Text.EndsWith("后续", StringComparison.Ordinal), "text insertion appends at empty selection");
        var end = editor.CaretPosition.GetInsertionPosition(LogicalDirection.Backward);
        editor.Selection.Select(end.GetPositionAtOffset(-2)!, end);
        Tests.Check(Equals(editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold)
            && Equals(editor.Selection.GetPropertyValue(TextElement.FontSizeProperty), 20d), "empty-selection formatting applies to subsequently typed text");
        Tests.Pump(); restored.Destroy();
        var wordWindow = new NoteWindow(new Note { Content = "abcdef" }, store); wordWindow.Show(); Tests.Pump(100);
        var word = wordWindow.Editor; var wordStart = word.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        word.CaretPosition = wordStart.GetPositionAtOffset(3)!; word.Focus();
        EditingCommands.ToggleBold.Execute(null, word);
        Tests.Check(word.Text == "abcdef" && new TextRange(word.Document.ContentStart, word.Document.ContentEnd).GetPropertyValue(TextElement.FontWeightProperty).Equals(FontWeights.Normal), "empty-selection format does not format existing word");
        word.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, new TextComposition(InputManager.Current, word, "新")) { RoutedEvent = TextCompositionManager.TextInputEvent }); Tests.Pump(100);
        Tests.Check(word.Text == "abc新def", "typing style inserts in middle of existing word");
        var wordEnd = word.CaretPosition; word.Selection.Select(wordEnd.GetNextInsertionPosition(LogicalDirection.Backward)!, wordEnd);
        Tests.Check(Equals(word.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold), "only newly typed middle-of-word character is bold"); wordWindow.Destroy();
        // Selection must survive consecutive format actions (A menu / RunKeepingSelection).
        var keep = new NoteWindow(new Note { Content = "ABCDE" }, store); keep.Show(); Tests.Pump(50);
        var keepEditor = keep.Editor; keepEditor.Focus();
        var k0 = keepEditor.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        keepEditor.Selection.Select(k0.GetPositionAtOffset(1)!, k0.GetPositionAtOffset(4)!);
        Tests.Check(keepEditor.Selection.Text == "BCD", "selection persistence targets BCD");
        keepEditor.RunKeepingSelection(() => EditingCommands.ToggleBold.Execute(null, keepEditor)); Tests.Pump(50);
        Tests.Check(keepEditor.Selection.Text == "BCD" && Equals(keepEditor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold), "selection remains after bold");
        keepEditor.RunKeepingSelection(() => EditingCommands.ToggleUnderline.Execute(null, keepEditor)); Tests.Pump(50);
        Tests.Check(keepEditor.Selection.Text == "BCD"
            && keepEditor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) is TextDecorationCollection u && u.Count > 0, "selection remains after underline");
        keepEditor.RunKeepingSelection(keepEditor.ToggleHighlight); Tests.Pump(50);
        Tests.Check(keepEditor.Selection.Text == "BCD" && keepEditor.IsHighlightActive(), "selection remains after highlight");
        keepEditor.RunKeepingSelection(keepEditor.ToggleHighlight); Tests.Pump(50);
        Tests.Check(keepEditor.Selection.Text == "BCD" && !keepEditor.IsHighlightActive(), "highlight toggles off while selection remains");
        keepEditor.SelectCharacterRange(1, 3); keep.ToggleFormatPopupForTest(); Tests.Pump(80);
        Tests.Check(keep.IsFormatPopupOpen && keepEditor.Selection.Text == "BCD", "A popup stays open without clearing selection");
        keepEditor.RunKeepingSelection(() => EditingCommands.ToggleBold.Execute(null, keepEditor)); Tests.Pump(80);
        Tests.Check(keep.IsFormatPopupOpen && keepEditor.Selection.Text == "BCD", "A popup stays open after applying bold");
        keep.ToggleFormatPopupForTest(); Tests.Pump(40);
        Tests.Check(!keep.IsFormatPopupOpen, "A popup toggles closed");
        // A menu must expose checklist with the other format chips (visible labels).
        var chips = keepEditor.BuildFormatPopupContent();
        var labels = new List<string>();
        void Collect(DependencyObject? node)
        {
            if (node == null) return;
            if (node is Button { Content: string text })
            {
                string label = text.Trim();
                if (label.Length > 0) labels.Add(label);
                Tests.Check(!text.Contains('✓'), "A menu chips never show checkmarks");
            }
            if (node is Panel panel) foreach (UIElement child in panel.Children) Collect(child);
            else if (node is Decorator decorator) Collect(decorator.Child);
            else if (node is ContentControl { Content: DependencyObject content }) Collect(content);
        }
        Collect(chips);
        string[] expected = {
            Loc.T("Format.Bold"), Loc.T("Format.Underline"), Loc.T("Format.Highlight"),
            Loc.T("Format.IncreaseSize"), Loc.T("Format.DecreaseSize"), Loc.T("Format.Checklist")
        };
        Tests.Check(string.Join("|", labels) == string.Join("|", expected), "A menu shows format chips including checklist: " + string.Join("|", labels));
        chips.Measure(new Size(260, 400)); chips.Arrange(new Rect(chips.DesiredSize)); chips.UpdateLayout();
        Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "outputs"));
        var menuBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(chips.DesiredSize.Width)),
            Math.Max(1, (int)Math.Ceiling(chips.DesiredSize.Height)),
            96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        menuBitmap.Render(chips);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(menuBitmap));
        using (var stream = File.Create(Path.Combine(Environment.CurrentDirectory, "outputs", "a-menu-preview.png")))
            encoder.Save(stream);
        var list = new NoteWindow(new Note { Content = "买牛奶" }, store); list.Show(); Tests.Pump(50);
        var listEditor = list.Editor; listEditor.Focus();
        listEditor.CaretPosition = listEditor.Document.ContentEnd; listEditor.ToggleTaskAtCaret(); Tests.Pump(30);
        var taskPara = TaskParagraph.ParagraphAt(listEditor.CaretPosition);
        Tests.Check(taskPara != null && TaskParagraph.GetIsTask(taskPara) && TaskParagraph.FindMarker(taskPara) != null, "task toggle adds marker on paragraph");
        var taskChips = listEditor.BuildFormatPopupContent();
        var taskButtons = new List<Button>();
        void CollectButtons(DependencyObject? node)
        {
            if (node == null) return;
            if (node is Button b) taskButtons.Add(b);
            if (node is Panel panel) foreach (UIElement child in panel.Children) CollectButtons(child);
            else if (node is Decorator decorator) CollectButtons(decorator.Child);
        }
        CollectButtons(taskChips);
        Tests.Check(taskButtons.Count >= 6
            && taskButtons.Take(5).All(b => !b.IsEnabled)
            && taskButtons[^1].IsEnabled
            && ((string)taskButtons[^1].Content!).Contains(Loc.T("Format.Checklist")), "A menu disables format chips inside task");
        // Sync Enter must create the next task marker immediately (no Dispatcher delay).
        var body = TaskParagraph.ContentStartAfterMarker(taskPara!);
        if (body != null) listEditor.CaretPosition = taskPara!.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
        listEditor.InsertTaskParagraphAfter(taskPara!);
        Tests.Pump(10);
        var afterEnter = TaskParagraph.ParagraphAt(listEditor.CaretPosition);
        Tests.Check(afterEnter != null && afterEnter != taskPara && TaskParagraph.GetIsTask(afterEnter) && TaskParagraph.FindMarker(afterEnter) != null,
            "Enter immediately creates next task with marker");
        Tests.Check(TaskParagraph.BodyIsEmpty(afterEnter!), "new task after Enter is empty");
        listEditor.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(listEditor)!, 0, Key.Back) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Tests.Pump(10);
        Tests.Check(!TaskParagraph.GetIsTask(afterEnter!) && TaskParagraph.FindMarker(afterEnter!) == null, "empty task backspace clears task state");
        // Entering checklist clears prior character formatting (cancel must not restore it).
        {
            var formatNote = new Note { Content = "买牛奶" }; store.Save(formatNote);
            var formatWin = new NoteWindow(formatNote, store); formatWin.Show(); Tests.Pump(40);
            var formatEditor = formatWin.Editor;
            formatEditor.Selection.Select(formatEditor.Document.ContentStart, formatEditor.Document.ContentEnd);
            EditingCommands.ToggleBold.Execute(null, formatEditor);
            formatEditor.ToggleHighlight();
            formatEditor.ChangeFontSize(1);
            Tests.Pump(20);
            formatEditor.CaretPosition = formatEditor.Document.ContentStart;
            formatEditor.ToggleTaskAtCaret(); Tests.Pump(30);
            var formattedTask = TaskParagraph.ParagraphAt(formatEditor.CaretPosition)!;
            var bodyStart = TaskParagraph.ContentStartAfterMarker(formattedTask)!;
            var bodyRange = new TextRange(bodyStart, formattedTask.ContentEnd);
            Tests.Check(TaskParagraph.GetIsTask(formattedTask), "formatted paragraph becomes task");
            Tests.Check(!Equals(bodyRange.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold), "task convert clears bold");
            object bg = bodyRange.GetPropertyValue(TextElement.BackgroundProperty);
            bool highlightGone = bg == DependencyProperty.UnsetValue || bg is null || Equals(bg, Brushes.Transparent)
                || (bg is SolidColorBrush solid && solid.Color.A == 0)
                || !formatEditor.IsHighlightActive();
            Tests.Check(highlightGone, "task convert clears highlight");
            object sizeAfter = bodyRange.GetPropertyValue(TextElement.FontSizeProperty);
            Tests.Check(sizeAfter is double fontSize && Math.Abs(fontSize - 16) < 0.1, "task convert resets font size");
            var convertMarker = TaskParagraph.FindMarker(formattedTask)!;
            Tests.Check(Math.Abs(convertMarker.Width - TaskMarker.Size) < 0.1 && Math.Abs(convertMarker.Height - TaskMarker.Size) < 0.1, "task marker is fixed 18x18");
            Tests.Check(Math.Abs(formattedTask.TextIndent + TaskMarker.Slot) < 0.1 && Math.Abs(formattedTask.Margin.Left - TaskMarker.Slot) < 0.1, "task hanging indent matches marker slot");
            Tests.Check(Math.Abs(formattedTask.LineHeight - TaskMarker.LineBoxHeight) < 0.1, "task uses fixed first-line box height");
            formatEditor.ToggleTaskAtCaret(); Tests.Pump(20);
            Tests.Check(!TaskParagraph.GetIsTask(TaskParagraph.ParagraphAt(formatEditor.CaretPosition)!), "task cancel leaves plain paragraph");
            var plainRange = new TextRange(formatEditor.Document.ContentStart, formatEditor.Document.ContentEnd);
            Tests.Check(!Equals(plainRange.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold), "cancel task does not restore prior bold");
            formatWin.Destroy();
        }
        // Marker stays on first line when body wraps (zh / ja / en).
        {
            var wrapStore = new NoteStore(Path.Combine(root, "task-wrap"));
            foreach (var sample in new[]
            {
                "明天去超市买牛奶然后准备面试资料还要把明天需要的文件全部整理好再出门",
                "明日の面接のために資料を準備して必要な書類を全部まとめてから出発する",
                "Prepare all interview materials before tomorrow morning and pack every document you need"
            })
            {
                var wrapNote = new Note { Content = sample, Width = 280, Height = 360 };
                wrapStore.Save(wrapNote);
                var wrapWin = new NoteWindow(wrapNote, wrapStore); wrapWin.Show(); Tests.Pump(50);
                wrapWin.Width = 280; wrapWin.Height = 360; Tests.Pump(30);
                var wrapEditor = wrapWin.Editor;
                wrapEditor.CaretPosition = wrapEditor.Document.ContentStart;
                wrapEditor.ToggleTaskAtCaret(); Tests.Pump(40);
                wrapEditor.UpdateLayout();
                var wrapPara = wrapEditor.Document.Blocks.OfType<Paragraph>().First(TaskParagraph.GetIsTask);
                var marker = TaskParagraph.FindMarker(wrapPara)!;
                Tests.Check(marker.Parent is InlineUIContainer host && host.BaselineAlignment == BaselineAlignment.Center,
                    "marker uses first-line BaselineAlignment.Center");
                var yShort = marker.TransformToAncestor(wrapEditor).Transform(new Point(0, 0)).Y;
                // Force more wrapping without changing marker metrics.
                wrapWin.Width = 200; Tests.Pump(60);
                wrapEditor.UpdateLayout();
                var yWrapped = marker.TransformToAncestor(wrapEditor).Transform(new Point(0, 0)).Y;
                Tests.Check(Math.Abs(yShort - yWrapped) < 1.5, "marker Y stable when task wraps: " + sample[..Math.Min(8, sample.Length)]);
                TaskParagraph.SetIsChecked(wrapPara, true); Tests.Pump(30);
                wrapEditor.UpdateLayout();
                var yChecked = marker.TransformToAncestor(wrapEditor).Transform(new Point(0, 0)).Y;
                Tests.Check(Math.Abs(yShort - yChecked) < 1.5 && Math.Abs(marker.Width - TaskMarker.Size) < 0.1, "checked look does not move/resize marker");
                TaskParagraph.SetIsChecked(wrapPara, false);
                wrapWin.Destroy();
            }
        }
        // Undo: typing / task toggle survive autosave serialization (SaveRtf must not clear undo).
        {
            var undoNote = new Note { Content = "" }; store.Save(undoNote);
            var undoWin = new NoteWindow(undoNote, store); undoWin.Show(); Tests.Pump(40);
            var undoEditor = undoWin.Editor;
            undoEditor.CaretPosition = undoEditor.Document.ContentStart;
            undoEditor.AppendText("abc"); Tests.Pump(20);
            Tests.Check(undoEditor.CanUndo, "typing enables undo");
            string savedXaml = undoEditor.SaveRtf(); // autosave path
            Tests.Check(undoEditor.CanUndo, "SaveRtf snapshot does not clear undo stack");
            string beforeUndo = undoEditor.Text;
            undoEditor.Undo(); Tests.Pump(20);
            Tests.Check(undoEditor.Text != beforeUndo || !beforeUndo.Contains('c'), "undo after save still runs");
            undoEditor.Document.Blocks.Clear();
            undoEditor.Document.Blocks.Add(new Paragraph(new Run("买牛奶")) { Margin = new Thickness(0) });
            Tests.Pump(20);
            // Reset undo boundary after programmatic replace, then toggle task.
            undoEditor.IsUndoEnabled = false; undoEditor.IsUndoEnabled = true;
            undoEditor.CaretPosition = undoEditor.Document.ContentStart;
            undoEditor.ToggleTaskAtCaret(); Tests.Pump(20);
            Tests.Check(TaskParagraph.GetIsTask(TaskParagraph.ParagraphAt(undoEditor.CaretPosition)!), "task for undo test");
            Tests.Check(undoEditor.HasUndo, "task toggle is undoable");
            undoEditor.PerformUndo(); Tests.Pump(40);
            var afterUndoTask = TaskParagraph.ParagraphAt(undoEditor.CaretPosition)
                ?? undoEditor.Document.Blocks.OfType<Paragraph>().FirstOrDefault();
            Tests.Check(afterUndoTask == null || !TaskParagraph.GetIsTask(afterUndoTask), "undo restores plain paragraph from task");
            undoWin.Destroy();
            _ = savedXaml;
        }
        // Persist checklist through XAML round-trip.
        TaskParagraph.SetIsTask(taskPara!, true); TaskParagraph.SetIsChecked(taskPara!, true); Tests.Pump(100);
        string payload = list.Editor.SaveRtf();
        string xaml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        Tests.Check(xaml.Contains("PaperNotes.Task", StringComparison.Ordinal), "serialized XAML embeds task tag");
        list.Note.RichTextRtfBase64 = payload; list.Note.FormatVersion = 2; list.Note.Content = list.Editor.Text;
        store.Save(list.Note);
        var savedList = store.Load().First(n => n.Id == list.Note.Id);
        Tests.Check(savedList.RichTextRtfBase64 != null && savedList.RichTextRtfBase64.Length > 0, "store kept rich payload");
        list.Destroy();
        var reopen = new NoteWindow(savedList, store); reopen.Show(); Tests.Pump(80);
        var reopenPara = reopen.Editor.Document.Blocks.OfType<Paragraph>().FirstOrDefault(p => TaskParagraph.GetIsTask(p) || (p.Tag is string t && t.StartsWith("PaperNotes.Task")));
        Tests.Check(reopenPara != null, "reopened document has a task paragraph");
        Tests.Check(reopenPara != null && TaskParagraph.GetIsChecked(reopenPara) && TaskParagraph.FindMarker(reopenPara) != null, "task checked state survives restart");
        reopen.Destroy();
        keep.Destroy();
        var damaged = new Note { Content = "正文不能丢\n日本語", RichTextRtfBase64 = "invalid-base64" };
        var fallback = new NoteWindow(damaged, store); fallback.Show();
        Tests.Check(fallback.Editor.Text == damaged.Content && fallback.Editor.LoadWarning != null, "invalid rich data falls back to complete plain text"); fallback.Destroy();
        var mismatch = new Note { Content = "newer plain text", RichTextRtfBase64 = saved.RichTextRtfBase64 };
        var mismatchEditor = new NoteEditor(mismatch);
        Tests.Check(mismatchEditor.Text == mismatch.Content && mismatchEditor.LoadWarning != null, "stale rich payload cannot overwrite newer plain content");
        // Font-size ladder: one step at a time, including off-ladder Word sizes.
        Tests.Check(NoteEditor.StepFontSize(16, 1) == 20 && NoteEditor.StepFontSize(20, 1) == 26
            && NoteEditor.StepFontSize(26, 1) == 26, "font size increases one ladder step");
        Tests.Check(NoteEditor.StepFontSize(26, -1) == 20 && NoteEditor.StepFontSize(20, -1) == 16
            && NoteEditor.StepFontSize(16, -1) == 13 && NoteEditor.StepFontSize(13, -1) == 13, "font size decreases one ladder step");
        Tests.Check(NoteEditor.StepFontSize(18, 1) == 20 && NoteEditor.StepFontSize(18, -1) == 16
            && NoteEditor.StepFontSize(22, 1) == 26 && NoteEditor.StepFontSize(22, -1) == 20, "off-ladder sizes snap then step");
        {
            var sizeWin = new NoteWindow(new Note { Content = "字号" }, store); sizeWin.Show(); Tests.Pump(40);
            var sizeEditor = sizeWin.Editor; sizeEditor.Focus();
            sizeEditor.SelectCharacterRange(0, 2);
            sizeEditor.ChangeFontSize(1); Tests.Pump(20);
            Tests.Check(Equals(sizeEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty), 20d), "first enlarge is 20 not max");
            sizeEditor.ChangeFontSize(1); Tests.Pump(20);
            Tests.Check(Equals(sizeEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty), 26d), "second enlarge reaches 26");
            sizeEditor.ChangeFontSize(-1); Tests.Pump(20);
            Tests.Check(Equals(sizeEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty), 20d), "shrink returns to 20 not mid-jump");
            // A-menu chip click must not double-apply when popup rebuilds.
            sizeEditor.SelectCharacterRange(0, 2);
            sizeEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, 16d);
            sizeWin.ToggleFormatPopupForTest(); Tests.Pump(40);
            Tests.Check(sizeWin.IsFormatPopupOpen, "size popup open for chip click");
            Button? enlarge = null;
            void FindEnlarge(DependencyObject? node)
            {
                if (node == null || enlarge != null) return;
                if (node is Button { Content: string text } b && text.Contains(Loc.T("Format.IncreaseSize"))) enlarge = b;
                if (node is Panel panel) foreach (UIElement child in panel.Children) FindEnlarge(child);
                else if (node is Decorator decorator) FindEnlarge(decorator.Child);
                else if (node is ContentControl { Content: DependencyObject content }) FindEnlarge(content);
            }
            if (sizeWin.FormatPopupChildForTest is DependencyObject popupRoot) FindEnlarge(popupRoot);
            Tests.Check(enlarge != null, "found increase-size chip");
            enlarge!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Tests.Pump(80); // includes Background deferred popup rebuild
            Tests.Check(Equals(sizeEditor.Selection.GetPropertyValue(TextElement.FontSizeProperty), 20d),
                "A-menu enlarge applies exactly one step after deferred rebuild");
            sizeWin.Destroy();
        }
        // Paste sanitize: Word-like LineHeight/indent/float sizes → stable layout, Unicode kept.
        {
            var pasted = new FlowDocument(new Paragraph(new Run("中文日本語English🎉"))
            {
                Margin = new Thickness(24, 8, 0, 8),
                Padding = new Thickness(4),
                TextIndent = 36,
                LineHeight = 48,
                FontSize = 28
            });
            ((Run)((Paragraph)pasted.Blocks.FirstBlock).Inlines.FirstInline).FontSize = 28;
            NoteEditor.SanitizePastedDocument(pasted);
            var para = (Paragraph)pasted.Blocks.FirstBlock;
            Tests.Check(Math.Abs(para.Margin.Left) < 0.1 && Math.Abs(para.TextIndent) < 0.1, "paste sanitize clears Word margins/indent");
            Tests.Check(para.ReadLocalValue(Paragraph.LineHeightProperty) == DependencyProperty.UnsetValue, "paste sanitize clears LineHeight");
            string pastedText = new TextRange(pasted.ContentStart, pasted.ContentEnd).Text;
            Tests.Check(pastedText.Contains("中文日本語English", StringComparison.Ordinal) && pastedText.Contains("🎉", StringComparison.Ordinal),
                "paste sanitize keeps CJK/emoji text");
            var run = (Run)para.Inlines.FirstInline;
            Tests.Check(Math.Abs(run.FontSize - 26) < 0.1, "paste sanitize snaps Word font size onto ladder");
            // InsertPlainText path (Ctrl+Shift+V) without touching the system clipboard.
            var plainWin = new NoteWindow(new Note { Content = "AB" }, store); plainWin.Show(); Tests.Pump(30);
            var plainEditor = plainWin.Editor;
            plainEditor.SelectCharacterRange(0, 2);
            plainEditor.InsertPlainText("纯文本\n第二行");
            Tests.Pump(20);
            Tests.Check(plainEditor.Text.Contains("纯文本", StringComparison.Ordinal) && plainEditor.Text.Contains("第二行", StringComparison.Ordinal),
                "InsertPlainText replaces selection with Unicode plain text");
            plainWin.Destroy();
        }
        CjkPasteAcceptance.Run(store);
    }
    public static void SelectFirst(NoteEditor editor, int count)
    {
        var start = editor.Document.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        editor.Selection.Select(start, start.GetPositionAtOffset(count)!);
    }
}
