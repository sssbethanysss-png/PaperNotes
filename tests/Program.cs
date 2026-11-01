using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using PaperNotes;

static class Tests
{
    public static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
    public static void Pump(int ms = 550)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--crash-writer")
        {
            var store = new NoteStore(args[1]);
            for (int i = 0; i < 5; i++) store.Save(new Note { Content = "durable " + i });
            Console.WriteLine("COMMITTED"); Console.Out.Flush(); System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
        }
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Dispatcher.BeginInvoke(new Action(() => app.Shutdown(Run())));
        Environment.ExitCode = app.Run();
    }
    static int Run()
    {
        try
        {
            Loc.ThrowIfCatalogInvalid();
            Check(Loc.Language == Loc.Ja, "default UI language is Japanese before preferences load");
            Check(Loc.T("App.Name") == "PaperNotes", "Japanese brand display name");
            Loc.SetLanguage(Loc.Zh, notify: false);
            Check(Loc.T("App.Name") == "纸间", "Chinese brand display name");
            Check(Loc.F("Notes.Count", ("notes.Length", 1)).Contains("1"), "zh count template substitutes");
            Loc.SetLanguage(Loc.En, notify: false);
            Check(Loc.F("Notes.Count", ("notes.Length", 1)) == "1 note", "english singular override for one note");
            Check(Loc.F("Notes.Count", ("notes.Length", 2)) == "2 notes", "english plural for notes");
            Loc.SetLanguage(Loc.Ja, notify: false);
            Check(Loc.NormalizeLanguage(null) == Loc.Ja && Loc.NormalizeLanguage("xx") == Loc.Ja, "unknown language falls back to Japanese");
            var root = Path.Combine(Environment.CurrentDirectory, "work", "tests-" + Guid.NewGuid().ToString("N"));
            var store = new NoteStore(root);
            var app = Application.Current;
            var legacyStore = new NoteStore(Path.Combine(root, "legacy"));
            const string legacyFile = "11111111-2222-3333-4444-555555555555.json";
            File.Copy(Path.Combine(Environment.CurrentDirectory, "tests", "Fixtures", legacyFile), Path.Combine(legacyStore.DirectoryPath, legacyFile));
            var legacy = legacyStore.Load().Single(); var legacyContent = legacy.Content;
            var legacyWindow = new NoteWindow(legacy, legacyStore); legacyWindow.Show(); legacyWindow.Flush(); legacyWindow.Destroy();
            Check(legacyStore.Load().Single().Content == legacyContent && legacy.IsPinned && legacy.Color == "Pink", "v1 fixture opens and saves without losing text or metadata");
            var compactStore = new NoteStore(Path.Combine(root, "compact")); var compactNote = new Note();
            var compact = new NoteWindow(compactNote, compactStore); compact.Show(); compact.Height = 48; Pump(100);
            Check(compact.IsCompact && compact.MinHeight == 40, "compact activates at one-line height");
            Check(compact.IsCompactDragVisible, "compact shows invisible top drag strip");
            var compactHwnd = new System.Windows.Interop.WindowInteropHelper(compact).Handle;
            Check((GetWindowLong(compactHwnd, -16) & 0x00010000) == 0, "note window clears WS_MAXIMIZEBOX to reduce snap maximize");
            compact.WindowState = WindowState.Minimized; Pump(50);
            compact.RestoreAndActivate(false); Pump(80);
            Check(compact.IsVisible && compact.WindowState != WindowState.Minimized, "RestoreAndActivate recovers minimized note");
            compact.SetTranslucent(true);
            Check(compact.Opacity == 0.85 && (GetWindowLong(compactHwnd, -20) & 0x80000) != 0, "WPF layered window opacity is 0.85");
            int cx = (int)compactNote.X + 2, cy = (int)compactNote.Y + 2;
            Check(SendMessage(compactHwnd, 0x0084, IntPtr.Zero, new IntPtr((cy << 16) | (cx & 0xffff))).ToInt32() == 13, "compact layered window retains native corner resize hit target");
            compact.Flush(); compact.Destroy();
            var compactReload = compactStore.Load().Single();
            var reopenedCompact = new NoteWindow(compactReload, compactStore); reopenedCompact.Show(); Pump(100);
            Check(compactReload.IsTranslucent && reopenedCompact.IsCompact && Math.Abs(reopenedCompact.Height - 48) < 2, "compact size and translucency survive reload");
            reopenedCompact.Height = 300; Pump(100); Check(!reopenedCompact.IsCompact && !reopenedCompact.IsCompactDragVisible, "toolbar returns when height expands"); reopenedCompact.Destroy();
            // Collapse / expand (Stickies WindowShade)
            var collapseStore = new NoteStore(Path.Combine(root, "collapse"));
            var collapseNote = new Note { Content = "明天面试\n第二行", Width = 500, Height = 400, ExpandedWidth = 500, ExpandedHeight = 400 };
            var collapseWin = new NoteWindow(collapseNote, collapseStore); collapseWin.Show(); Pump(80);
            collapseWin.Width = 500; collapseWin.Height = 400; Pump(40);
            collapseWin.ToggleCollapseForTest(); Pump(250);
            Check(collapseNote.IsCollapsed && Math.Abs(collapseWin.Width - Note.CollapsedWidth) < 1 && Math.Abs(collapseWin.Height - Note.CollapsedHeight) < 1, "collapse locks to 280x42");
            Check(Math.Abs(collapseNote.ExpandedWidth - 500) < 1 && Math.Abs(collapseNote.ExpandedHeight - 400) < 1, "collapse remembers expanded size");
            Check(!collapseWin.IsCompact, "collapsed is not compact mode");
            collapseWin.Flush(); collapseWin.Destroy();
            var collapseReload = collapseStore.Load().Single();
            Check(collapseReload.IsCollapsed && Math.Abs(collapseReload.Width - Note.CollapsedWidth) < 1, "collapsed state persists");
            var collapseReopen = new NoteWindow(collapseReload, collapseStore); collapseReopen.Show(); Pump(120);
            Check(Math.Abs(collapseReopen.Width - Note.CollapsedWidth) < 2 && Math.Abs(collapseReopen.Height - Note.CollapsedHeight) < 2, "reopen stays collapsed size");
            collapseReopen.ToggleCollapseForTest(); Pump(250);
            Check(!collapseReload.IsCollapsed && Math.Abs(collapseReopen.Width - 500) < 2 && Math.Abs(collapseReopen.Height - 400) < 2, "expand restores expanded size");
            collapseReopen.Destroy();
            // First-line rich clone: keep formatting, stop at LineBreak, skip second line
            var previewDoc = new System.Windows.Documents.FlowDocument();
            var previewPara = new System.Windows.Documents.Paragraph();
            previewPara.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run("明天")));
            var hiRun = new System.Windows.Documents.Run("面试") { Background = System.Windows.Media.Brushes.Yellow, FontSize = 20 };
            previewPara.Inlines.Add(hiRun);
            previewPara.Inlines.Add(new System.Windows.Documents.LineBreak());
            previewPara.Inlines.Add(new System.Windows.Documents.Run("第二行不应出现"));
            previewDoc.Blocks.Add(previewPara);
            var previewTb = new System.Windows.Controls.TextBlock();
            CollapsePreview.Fill(previewTb, previewDoc);
            string previewText = new System.Windows.Documents.TextRange(previewTb.ContentStart, previewTb.ContentEnd).Text;
            Check(previewText == "明天面试", "collapse preview is first line only");
            Check(previewTb.Inlines.OfType<System.Windows.Documents.Bold>().Any(), "collapse preview keeps bold");
            Check(previewTb.Inlines.OfType<System.Windows.Documents.Run>().Any(r => Equals(r.Background, System.Windows.Media.Brushes.Yellow) && Math.Abs(r.FontSize - 20) < 0.1), "collapse preview keeps highlight and font size");
            var emptyFirst = new System.Windows.Documents.FlowDocument(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("")));
            emptyFirst.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("第二行")));
            var emptyTb = new System.Windows.Controls.TextBlock();
            CollapsePreview.Fill(emptyTb, emptyFirst);
            Check(string.IsNullOrEmpty(new System.Windows.Documents.TextRange(emptyTb.ContentStart, emptyTb.ContentEnd).Text), "empty first line stays blank");
            HotkeyFieldTests.Run();
            RichTextTests.Run(root);
            TxtTests.Run(root);
            CategoryTests.Run(root);
            var note = new Note(); store.Save(note);
            var window = new NoteWindow(note, store); window.Show();
            window.Editor.Text = "中文便签\n日本語メモ\nEnglish"; Pump();
            Check(store.Load().Single().Content == window.Editor.Text, "400ms autosave / Unicode");
            window.Editor.AppendText(" + close flush"); window.Close();
            Check(store.Load().Single().Content.EndsWith("close flush"), "close flushes pending edits");
            Check(!window.IsVisible && note.IsHidden, "X hides without deleting"); window.Destroy();
            note.Content = "backup"; store.Save(note); note.Content = "latest"; store.Save(note);
            File.WriteAllText(Path.Combine(root, note.Id + ".json"), "broken");
            Check(store.Load().Single().Content == "backup" && store.Warnings.Count > 0, "corrupt primary recovers backup");
            var multiStore = new NoteStore(Path.Combine(root, "multi"));
            var manager = new NoteManager(multiStore);
            for (int i = 0; i < 5; i++)
            {
                var n = manager.Create(); var w = manager.Windows[n.Id];
                w.Editor.Text = "Note " + i; w.Left = 80 + i * 35; w.Top = 80 + i * 25; w.Width = 260 + i * 20;
            }
            Pump(); Check(manager.Windows.Count == 5, "five independent windows");
            var before = multiStore.Load(); manager.CloseAll();
            var restart = new NoteManager(multiStore); restart.Restore(); Pump();
            Check(restart.Notes.Count == 5 && restart.Notes.All(n => n.Content.StartsWith("Note ")), "five notes survive restart");
            Check(restart.Notes.All(n => Math.Abs(n.Width - before.Single(b => b.Id == n.Id).Width) < 2 && Math.Abs(n.X - before.Single(b => b.Id == n.Id).X) < 2), "geometry survives restart");
            var first = restart.Notes[0]; var firstWindow = restart.Windows[first.Id];
            firstWindow.SetTopmost(true); firstWindow.SetPinned(false);
            var topHandle = new System.Windows.Interop.WindowInteropHelper(firstWindow).Handle;
            var ordinary = new Window { Title = "PaperNotes test: ordinary application", Width = 500, Height = 350, Left = firstWindow.Left, Top = firstWindow.Top };
            ordinary.Show(); ordinary.Activate(); Pump(150);
            var otherHandle = new System.Windows.Interop.WindowInteropHelper(ordinary).Handle;
            Check(ordinary.IsActive && !firstWindow.IsActive && GetActiveWindow() == otherHandle && (GetWindowLong(topHandle, -20) & 8) != 0 && IsAbove(topHandle, otherHandle), "topmost stays above active ordinary window without taking focus");
            firstWindow.Hide(); firstWindow.Show(); firstWindow.WindowState = WindowState.Minimized; firstWindow.WindowState = WindowState.Normal;
            firstWindow.EnsureVisible(); ordinary.Activate(); Pump(150);
            Check(ordinary.IsActive && GetActiveWindow() == otherHandle && IsAbove(topHandle, otherHandle) && (GetWindowLong(topHandle, -20) & 8) != 0, "topmost survives hide/show/minimize/placement while peer keeps focus");
            firstWindow.SetTopmost(false); Check((GetWindowLong(topHandle, -20) & 8) == 0, "disabling topmost clears native extended style");
            firstWindow.SetTopmost(true); ordinary.Close();
            var peerWindow = restart.Windows[restart.Notes[1].Id];
            peerWindow.SetTopmost(true); Pump(40);
            var peerHandle = new System.Windows.Interop.WindowInteropHelper(peerWindow).Handle;
            peerWindow.Activate(); Pump(80);
            Check(peerWindow.IsActive && IsAbove(peerHandle, topHandle) && (GetWindowLong(topHandle, -20) & 8) != 0 && (GetWindowLong(peerHandle, -20) & 8) != 0, "activating a topmost note raises it above other topmost notes");
            firstWindow.Activate(); Pump(80);
            Check(firstWindow.IsActive && IsAbove(topHandle, peerHandle), "activating the other topmost note raises it back");
            firstWindow.SetTopmost(false); peerWindow.SetTopmost(false); Pump(40);
            var plainWindow = restart.Windows[restart.Notes[2].Id];
            var plainHandle = new System.Windows.Interop.WindowInteropHelper(plainWindow).Handle;
            plainWindow.Activate(); Pump(60);
            peerWindow.Activate(); Pump(80);
            Check(peerWindow.IsActive && IsAbove(peerHandle, plainHandle) && (GetWindowLong(peerHandle, -20) & 8) == 0, "activating a normal note raises it above other normal notes");
            firstWindow.SetTopmost(true);
            Check(firstWindow.Topmost && !first.IsPinned && multiStore.Load().Single(n => n.Id == first.Id).IsAlwaysOnTop, "topmost persists independently from index pin");
            firstWindow.Close(); restart.HideAll(); restart.ShowAll();
            Check(!firstWindow.IsVisible && restart.Windows.Values.Count(w => w.IsVisible) == 4, "bulk show preserves individually hidden notes");
            Check(restart.Open(first) == firstWindow && firstWindow.IsVisible, "reopen hidden note reuses window");
            firstWindow.WindowState = WindowState.Minimized; restart.Open(first);
            Check(firstWindow.WindowState == WindowState.Normal, "index restores a minimized note");
            restart.Delete(first, false); Check(multiStore.Load().Count == 4, "delete removes note durably");
            var index = new IndexWindow(restart); index.Show(); Pump(100);
            Check(restart.Query("nOtE", "All").Length == 4 && restart.Query("不存在", "All").Length == 0, "index full text search");
            restart.Windows[restart.Notes[0].Id].SetPinned(true);
            Check(restart.Query("", "Pinned").Length == 1 && restart.Query("", "All")[0].IsPinned, "index pinned filter and ordering");
            index.Close(); Check(!index.IsVisible && restart.Windows.Count == 4, "closing index keeps notes alive");
            index.AllowClose = true; index.Close();
            int created = 0, toggled = 0;
            using (var keys = new Hotkeys(() => created++, () => toggled++))
            {
                // Use uncommon chords so a running PaperNotes / other apps do not steal the test bindings.
                var registered = keys.Apply("Ctrl+Alt+Shift+F9", "Ctrl+Alt+Shift+F10");
                Check(registered.Ok, "explicit hotkeys register for test: " + string.Join("; ", registered.Failures));
                SendMessage(keys.Handle, 0x0312, new IntPtr(1), IntPtr.Zero);
                SendMessage(keys.Handle, 0x0312, new IntPtr(2), IntPtr.Zero);
                Check(created == 1 && toggled == 1, "native hotkey message dispatch");
                using var conflict = new Hotkeys(() => { }, () => { });
                var conflicted = conflict.Apply("Ctrl+Alt+Shift+F9", "Ctrl+Alt+Shift+F10");
                Check(!conflicted.Ok && conflicted.Failures.Count >= 1, "hotkey conflicts handled without crash");
                Check(keys.Apply(null, null).Ok && keys.AppliedCreate == null, "clearing hotkeys unregisters bindings");
            }
            using (var tray = new Tray(restart, () => { }, () => { })) { Pump(100); Check(true, "tray creates and disposes"); }
            using (var primary = new SingleInstance(root))
            {
                int activated = 0; primary.Listen(app.Dispatcher, () => activated++);
                using var secondary = new SingleInstance(root); Pump(150);
                Check(primary.IsPrimary && !secondary.IsPrimary && activated == 1, "single instance activates existing process");
            }
            restart.CloseAll();
            var settingsStore = new SettingsStore(Path.Combine(root, "preferences"));
            var preferences = new Preferences { DefaultColor = "Green", DefaultAlwaysOnTop = true, AllHidden = true };
            settingsStore.Save(preferences); var readSettings = settingsStore.Load();
            var configured = new NoteManager(multiStore, readSettings); configured.Restore();
            Check(configured.Windows.Count == 0, "bulk hidden state survives restart");
            var configuredNote = configured.Create();
            Check(configuredNote.Color == "Green" && configuredNote.IsAlwaysOnTop, "new notes honor saved defaults");
            Check(Startup.Command(@"C:\Program Files\PaperNotes\PaperNotes.exe") == "\"C:\\Program Files\\PaperNotes\\PaperNotes.exe\" --startup", "startup command safely quotes spaces");
            configured.CloseAll();
            var area = new System.Drawing.Rectangle(0, 0, 1920, 1080);
            var fit = WindowPlacement.Fit(new System.Drawing.Rectangle(-2400, -1800, 300, 300), area);
            Check(area.Contains(fit), "disconnected monitor geometry returns to visible work area");
            var negativeMonitor = new System.Drawing.Rectangle(-1920, 0, 1920, 1080);
            Check(WindowPlacement.Fit(new System.Drawing.Rectangle(-1800, 100, 300, 300), negativeMonitor).X == -1800, "negative monitor coordinates preserved");
            foreach (var scale in new[] { 1.25, 1.5, 2.0 })
                Check(area.Contains(WindowPlacement.Fit(new System.Drawing.Rectangle(1800, 900, (int)(300 * scale), (int)(300 * scale)), area)), "scaled bounds clamp " + scale);
            var failureStore = new NoteStore(Path.Combine(root, "failure")); var failureNote = new Note { Content = "original" }; failureStore.Save(failureNote);
            var failureWindow = new NoteWindow(failureNote, failureStore); failureWindow.Show();
            var failurePath = Path.Combine(failureStore.DirectoryPath, failureNote.Id + ".json");
            using (var locked = new FileStream(failurePath + ".tmp", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                failureWindow.Editor.Text = "pending";
                Check(!failureWindow.Flush(), "write error reported without overwriting prior data");
                Check(failureStore.Load().Single().Content == "original", "failed write preserves committed version");
            }
            Check(failureWindow.Flush() && failureStore.Load().Single().Content == "pending", "pending content saves after storage recovers"); failureWindow.Destroy();
            var crashDir = Path.Combine(root, "crash");
            var psi = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            psi.ArgumentList.Add("--crash-writer"); psi.ArgumentList.Add(crashDir);
            using (var child = System.Diagnostics.Process.Start(psi)!)
            {
                var ready = child.StandardOutput.ReadLineAsync();
                if (!ready.Wait(10000)) { child.Kill(); throw new Exception("Crash writer timed out"); }
                Check(ready.Result == "COMMITTED", "child process committed notes"); child.Kill(); child.WaitForExit();
            }
            Check(new NoteStore(crashDir).Load().Count == 5, "five notes survive forced process termination");
            var offscreen = new Note { X = -100000, Y = -100000 }; var offscreenWindow = new NoteWindow(offscreen, store); offscreenWindow.Show(); Pump(150);
            Check(offscreen.X > -100000 && offscreen.Y > -100000, "actual offscreen WPF window recovered"); offscreenWindow.Destroy();
            var searchManager = new NoteManager(new NoteStore(Path.Combine(root, "search")));
            for (int i = 0; i < 300; i++) searchManager.Notes.Add(new Note { Content = "项目计划 日本語 English " + i });
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) searchManager.Query("日本語", "All");
            stopwatch.Stop(); Check(stopwatch.ElapsedMilliseconds < 2000, "300-note search x1000: " + stopwatch.ElapsedMilliseconds + "ms");
            var demoManager = new NoteManager(new NoteStore(Path.Combine(root, "preview")));
            var demoNote = demoManager.Create(); var demoWindow = demoManager.Windows[demoNote.Id];
            demoWindow.Editor.Text = "给想法留一点空间\n\n每一个灵感，都值得被记住。\n\n周五之前\n· 整理本周阅读笔记\n· 给书桌添一盆绿植\n· 留一点时间，散散步";
            demoWindow.Width = 340; demoWindow.Height = 360; demoWindow.SetPinned(true); demoWindow.SetTopmost(true);
            RichTextTests.SelectFirst(demoWindow.Editor, 8);
            System.Windows.Documents.EditingCommands.ToggleBold.Execute(null, demoWindow.Editor); demoWindow.Editor.ChangeFontSize(1);
            demoWindow.Editor.Selection.Select(demoWindow.Editor.Document.ContentEnd, demoWindow.Editor.Document.ContentEnd);
            var second = demoManager.Create(); demoManager.Windows[second.Id].Editor.Text = "读书摘记\n慢下来，才能看见细节。"; demoManager.Windows[second.Id].SetColor("Green");
            var third = demoManager.Create(); demoManager.Windows[third.Id].Editor.Text = "週末のアイデア\n散歩、喫茶店、新しい本。"; demoManager.Windows[third.Id].SetColor("Blue"); demoManager.Windows[third.Id].Close();
            var researchCat = demoManager.AddCategory("研究", "#7C9A62");
            var essayCat = demoManager.AddCategory("随笔", "#C4924A");
            demoManager.AddCategory("待办", "#6A94B8");
            demoManager.AssignCategory(new[] { demoNote }, researchCat!.Id);
            demoManager.AssignCategory(new[] { second }, essayCat!.Id);
            Pump(); var preview = new IndexWindow(demoManager); preview.Show(); Pump(150);
            Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "outputs"));
            Capture(preview, "index-preview.png"); Capture(demoWindow, "note-preview.png");
            demoWindow.Height = 48; Pump(100); Capture(demoWindow, "compact-preview.png");
            Check(preview.IsVisible && demoWindow.IsVisible, "real WPF windows rendered for visual review");
            preview.AllowClose = true; preview.Close(); demoManager.CloseAll();
            Console.WriteLine("ALL TESTS PASSED"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wp, IntPtr lp);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    private static bool IsAbove(IntPtr upper, IntPtr lower)
    {
        for (var h = GetWindow(lower, 3); h != IntPtr.Zero; h = GetWindow(h, 3)) if (h == upper) return true;
        return false;
    }
    private static void Capture(Window window, string name)
    {
        window.UpdateLayout(); var visual = (FrameworkElement)window.Content;
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth), (int)Math.Ceiling(visual.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var drawing = new System.Windows.Media.DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            var rect = new Rect(0, 0, visual.ActualWidth, visual.ActualHeight);
            dc.DrawRectangle(window.Background, null, rect);
        }
        bitmap.Render(drawing); bitmap.Render(visual);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(Environment.CurrentDirectory, "outputs", name)); encoder.Save(file);
    }
}
