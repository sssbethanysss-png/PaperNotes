using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using PaperNotes;

/// <summary>
/// Distinguishes character-data corruption vs display-only issues for mixed ZH/JA/EN paste.
/// Does not touch the system clipboard.
/// </summary>
static class CjkPasteAcceptance
{
    // Same mixed body used for Word-rich / plain / notepad-style paths.
    public const string Source =
        "日本人の父と中国人の母の間に生まれ、日本語と中文とEnglishを学び、課題を解決する。\n" +
        "为什么选python，明明java用的更多？研究しています。貴重な学び🎉";

    public static void Run(NoteStore store)
    {
        string expected = NoteEditor.Normalize(Source);
        AssertCodePoints(expected, "源文字", new[] { "日", "本", "語", "課", "題", "研", "究", "貴", "🎉" });

        // 1) Word-like rich paste: RTF characters intentionally damaged, UnicodeText holds the truth.
        {
            var data = new DataObject();
            data.SetData(DataFormats.Rtf, BuildDamagedWordRtf());
            data.SetData(DataFormats.UnicodeText, Source);
            Tests.Check(NoteEditor.TryPreparePaste(data, forcePlain: false, out var prepared, out var format, out var mode),
                "Word-like paste prepares a payload");
            Tests.Check(mode == "plain-fallback-mismatch", "damaged RTF falls back because Unicode mismatches: " + mode);
            Tests.Check(format == DataFormats.UnicodeText, "mismatch uses UnicodeText, not damaged XAML");
            var win = new NoteWindow(new Note { Content = "" }, store); win.Show(); Tests.Pump(40);
            var editor = win.Editor; editor.SelectAll();
            editor.ApplyPreparedPaste(prepared, format); Tests.Pump(40);
            AssertEditorMatches(editor, expected, "Word-normal path after RTF/Unicode mismatch");
            editor.SelectAll(); editor.ChangeFontSize(1); Tests.Pump(20);
            AssertEditorMatches(editor, expected, "after font enlarge");
            editor.ChangeFontSize(-1); Tests.Pump(20);
            AssertEditorMatches(editor, expected, "after font shrink");
            AssertDisplayHealthy(editor, expected, "Word-normal display");
            win.Note.Content = editor.Text;
            win.Note.RichTextRtfBase64 = editor.SaveRtf();
            win.Note.FormatVersion = 2;
            store.Save(win.Note);
            var reloaded = store.Load().Single(n => n.Id == win.Note.Id);
            win.Destroy();
            var reopen = new NoteWindow(reloaded, store); reopen.Show(); Tests.Pump(80);
            AssertEditorMatches(reopen.Editor, expected, "save/reopen after Word-normal path");
            AssertDisplayHealthy(reopen.Editor, expected, "reopen display");
            reopen.Destroy();
        }

        // 1b) Matching RTF + Unicode keeps rich path without altering code points.
        {
            var data = new DataObject();
            data.SetData(DataFormats.Rtf, BuildMatchingWordRtf(Source));
            data.SetData(DataFormats.UnicodeText, Source);
            Tests.Check(NoteEditor.TryPreparePaste(data, false, out var prepared, out var format, out var mode),
                "matching Word RTF prepares");
            Tests.Check(mode == "rich-rtf" && format == DataFormats.Xaml, "matching RTF stays rich: " + mode);
            var win = new NoteWindow(new Note { Content = "" }, store); win.Show(); Tests.Pump(30);
            win.Editor.SelectAll();
            win.Editor.ApplyPreparedPaste(prepared, format); Tests.Pump(40);
            AssertEditorMatches(win.Editor, expected, "matching Word RTF paste");
            AssertDisplayHealthy(win.Editor, expected, "matching RTF display");
            win.Destroy();
        }

        // 2) Plain-text paste (Ctrl+Shift+V / force plain).
        {
            var data = new DataObject();
            data.SetData(DataFormats.Rtf, BuildDamagedWordRtf());
            data.SetData(DataFormats.UnicodeText, Source);
            Tests.Check(NoteEditor.TryPreparePaste(data, forcePlain: true, out var prepared, out var format, out var mode),
                "plain paste prepares");
            Tests.Check(mode == "plain-forced" && format == DataFormats.UnicodeText, "plain paste ignores RTF: " + mode);
            var win = new NoteWindow(new Note { Content = "旧" }, store); win.Show(); Tests.Pump(30);
            win.Editor.SelectAll();
            win.Editor.ApplyPreparedPaste(prepared, format); Tests.Pump(30);
            AssertEditorMatches(win.Editor, expected, "plain-text paste path");
            win.Destroy();
        }

        // 3) Notepad intermediary = UnicodeText only (no RTF).
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, Source);
            Tests.Check(NoteEditor.TryPreparePaste(data, false, out var prepared, out var format, out var mode),
                "notepad-style paste prepares");
            Tests.Check(format == DataFormats.UnicodeText, "notepad path uses UnicodeText");
            Tests.Check(mode is "plain-fallback" or "plain-fallback-mismatch", "notepad mode: " + mode);
            var win = new NoteWindow(new Note { Content = "" }, store); win.Show(); Tests.Pump(30);
            win.Editor.SelectAll();
            win.Editor.ApplyPreparedPaste(prepared, format); Tests.Pump(30);
            AssertEditorMatches(win.Editor, expected, "notepad intermediary paste");
            win.Note.Content = win.Editor.Text;
            win.Note.RichTextRtfBase64 = win.Editor.SaveRtf();
            win.Note.FormatVersion = 2;
            store.Save(win.Note);
            var id = win.Note.Id;
            win.Destroy();
            var reopen = new NoteWindow(store.Load().Single(n => n.Id == id), store); reopen.Show(); Tests.Pump(60);
            AssertEditorMatches(reopen.Editor, expected, "notepad path save/reopen");
            reopen.Destroy();
        }

        // Observed user corruption must be classified as data damage, not hidden by stripping.
        Tests.Check(Source.Contains("本", StringComparison.Ordinal) && Source.Contains("題", StringComparison.Ordinal),
            "source still contains 本/題 (no strip-as-fix)");
        Tests.Check(!expected.Contains("・{", StringComparison.Ordinal) && !expected.Contains("?}", StringComparison.Ordinal),
            "acceptance source is not the corrupted ・{ / ?} form");
    }

    static void AssertEditorMatches(NoteEditor editor, string expected, string label)
    {
        string actual = NoteEditor.Normalize(editor.Text);
        if (!NoteEditor.PlainTextMatches(actual, expected))
        {
            string diff = DescribeDiff(expected, actual);
            throw new Exception(label + " text mismatch. " + diff);
        }
        Tests.Check(true, label + " plain text matches source code points");
    }

    static void AssertDisplayHealthy(NoteEditor editor, string expected, string label)
    {
        editor.UpdateLayout();
        var doc = editor.Document;
        Tests.Check(double.IsNaN(doc.PageWidth), label + " PageWidth cleared so text can wrap");
        foreach (var paragraph in doc.Blocks.OfType<Paragraph>())
        {
            Tests.Check(paragraph.ReadLocalValue(Paragraph.LineHeightProperty) == DependencyProperty.UnsetValue,
                label + " no fixed LineHeight");
            Tests.Check(Math.Abs(paragraph.TextIndent) < 0.1, label + " no Word TextIndent");
        }
        // Glyph availability: every source char should measure with non-zero width (else display-missing).
        var typeface = new Typeface(editor.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        foreach (var rune in expected.EnumerateRunes())
        {
            if (Rune.IsControl(rune) || rune.Value is '\n' or '\r') continue;
            var ft = new FormattedText(
                rune.ToString(),
                CultureInfo.GetCultureInfo("ja-JP"),
                FlowDirection.LeftToRight,
                typeface,
                editor.FontSize,
                Brushes.Black,
                1.25);
            Tests.Check(ft.Width > 0.1, label + " glyph visible for U+" + rune.Value.ToString("X4"));
        }
    }

    static void AssertCodePoints(string text, string label, string[] mustContain)
    {
        foreach (string piece in mustContain)
        {
            Tests.Check(text.Contains(piece, StringComparison.Ordinal), label + " contains " + piece);
            if (piece.Length == 1)
                Tests.Check(text.Any(ch => ch == piece[0]), label + " code point U+" + ((int)piece[0]).ToString("X4"));
        }
    }

    static string DescribeDiff(string expected, string actual)
    {
        int n = Math.Min(expected.Length, actual.Length);
        int i = 0;
        while (i < n && expected[i] == actual[i]) i++;
        if (i == n && expected.Length == actual.Length) return "no char diff";
        string ExpAt(string s, int index) => index < s.Length
            ? $"U+{((int)s[index]):X4} '{s[index]}'"
            : "(end)";
        return $"first diff at {i}: expected {ExpAt(expected, i)} vs actual {ExpAt(actual, i)}; " +
               $"expectedLen={expected.Length} actualLen={actual.Length}";
    }

    /// <summary>RTF whose decoded text is the known corrupted form (本→・{, 題→?}).</summary>
    static string BuildDamagedWordRtf()
    {
        string damaged = Source.Replace("本", "・{", StringComparison.Ordinal).Replace("題", "?}", StringComparison.Ordinal);
        return BuildMatchingWordRtf(damaged);
    }

    static string BuildMatchingWordRtf(string text)
    {
        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi\ansicpg932\deff0{\fonttbl{\f0\fnil\fcharset128 Yu Gothic;}}\f0\fs24 ");
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n') { sb.Append(@"\par "); continue; }
            if (rune.Value == '\r') continue;
            if (rune.Value is >= 0x20 and < 0x7F && rune.Value is not '\\' and not '{' and not '}')
            {
                sb.Append((char)rune.Value);
                continue;
            }
            // RTF \u is 16-bit signed; supplementary chars must be surrogate pairs.
            foreach (char unit in rune.ToString())
            {
                int signed = unit > 32767 ? unit - 65536 : unit;
                sb.Append(@"\u").Append(signed).Append('?');
            }
        }
        sb.Append('}');
        return sb.ToString();
    }
}
