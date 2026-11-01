using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using PaperNotes;

static class TxtTests
{
    public static void Run(string root)
    {
        string folder = Path.Combine(root, "txt"); Directory.CreateDirectory(folder);
        const string text = "面试准备\r\n日本語 English 中文\r\n\r\n";
        var utf8 = Path.Combine(folder, "utf8.txt"); var bom = Path.Combine(folder, "bom.txt"); var utf16 = Path.Combine(folder, "utf16.txt");
        File.WriteAllText(utf8, text, new UTF8Encoding(false)); File.WriteAllText(bom, text, new UTF8Encoding(true)); File.WriteAllText(utf16, text, Encoding.Unicode);
        Tests.Check(TxtTransfer.Read(utf8) == text && TxtTransfer.Read(bom) == text && TxtTransfer.Read(utf16) == text, "TXT UTF8/BOM/UTF16 Unicode and line endings round-trip");
        var store = new NoteStore(Path.Combine(folder, "notes")); var manager = new NoteManager(store);
        var original = manager.Create("existing note");
        var imported = new[] { utf8, bom, utf16 }.Select(path => manager.Create(TxtTransfer.Read(path))).ToArray();
        Tests.Check(store.Load().Count == 4 && imported.Select(n => n.Id).Distinct().Count() == 3
            && imported.All(n => n.Content == text) && original.Content == "existing note", "each TXT import creates a durable new ID without overwriting notes");
        string existing = Path.Combine(folder, "面试准备.txt"); File.WriteAllText(existing, "keep me");
        string one = TxtTransfer.ExportUnique(imported[0], folder), two = TxtTransfer.ExportUnique(imported[1], folder);
        Tests.Check(Path.GetFileName(one) == "面试准备 (2).txt" && Path.GetFileName(two) == "面试准备 (3).txt" && File.ReadAllText(existing) == "keep me", "multi-export suffixes preserve existing and duplicate filenames");
        Tests.Check(File.ReadAllBytes(one).SequenceEqual(new UTF8Encoding(false).GetBytes(text)), "TXT export contains only exact body in UTF8");
        string single = Path.Combine(folder, "single.txt"); TxtTransfer.ExportSingle(imported[0], single);
        Tests.Check(File.ReadAllText(single) == text && Directory.GetFiles(folder, ".papernotes-*.tmp").Length == 0, "single export commits complete text and cleans temporary file");
        Tests.Check(TxtTransfer.FileStem(new Note { Content = "\nsecond line" }) == "Untitled Note"
            && TxtTransfer.FileStem(new Note { Content = "CON" }) == "_CON"
            && !TxtTransfer.FileStem(new Note { Content = "bad:/name?*" }).Any(Path.GetInvalidFileNameChars().Contains), "export titles handle blank first lines, reserved names and invalid characters");
        Directory.CreateDirectory(Path.Combine(folder, "directory.txt"));
        Tests.Check(Path.GetFileName(TxtTransfer.ExportUnique(new Note { Content = "directory" }, folder)) == "directory (2).txt", "export avoids existing directory names");
        string invalid = Path.Combine(folder, "invalid.txt"); File.WriteAllBytes(invalid, new byte[] { 0xff, 0xff, 0xc0 });
        bool rejected = false; try { TxtTransfer.Read(invalid); } catch (DecoderFallbackException) { rejected = true; }
        Tests.Check(rejected, "invalid UTF8 is rejected rather than imported as replacement characters");
        var index = new IndexWindow(manager); index.Show(); Tests.Pump(100);
        var list = (ListBox)index.FindName("NotesList");
        list.SelectedItems.Add(imported[0]); list.SelectedItems.Add(imported[1]);
        manager.Windows[imported[0].Id].SetPinned(true);
        Tests.Check(list.SelectionMode == SelectionMode.Extended && list.SelectedItems.Count == 2 && ((Button)index.FindName("ExportButton")).IsEnabled, "index multi-selection survives pinned reorder and enables export");
        manager.Windows[imported[2].Id].Close();
        Tests.Check(list.Items.Count == 4 && imported[2].IsHidden && list.SelectedItems.Count == 2, "hidden notes remain in the only list without losing selection");
        ((TextBox)index.FindName("SearchBox")).Text = "existing";
        Tests.Check(list.SelectedItems.Count == 0 && !((Button)index.FindName("ExportButton")).IsEnabled, "search removes filtered selections to prevent accidental export");
        index.AllowClose = true; index.Close(); manager.CloseAll();
    }
}
