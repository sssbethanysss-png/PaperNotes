using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PaperNotes;

public sealed class Hotkeys : IDisposable
{
    public const int IdCreate = 1;
    public const int IdToggle = 2;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    private readonly HwndSource source;
    private readonly Action create, toggle;
    private readonly HashSet<int> registered = new();
    private string? appliedCreate;
    private string? appliedToggle;
    public IntPtr Handle => source.Handle;
    public string? AppliedCreate => appliedCreate;
    public string? AppliedToggle => appliedToggle;
    public Hotkeys(Action create, Action toggle)
    {
        this.create = create; this.toggle = toggle;
        source = new HwndSource(new HwndSourceParameters("PaperNotes.Hotkeys") { ParentWindow = new IntPtr(-3), Width = 0, Height = 0 });
        source.AddHook(Hook);
    }
    /// <summary>Replace live hotkey registrations. Null/empty chords unregister that action.</summary>
    public HotkeyApplyResult Apply(string? createChord, string? toggleChord)
    {
        createChord = NormalizeStored(createChord);
        toggleChord = NormalizeStored(toggleChord);
        if (!HotkeyChord.IsUnset(createChord) && string.Equals(createChord, toggleChord, StringComparison.OrdinalIgnoreCase))
            return HotkeyApplyResult.Failed(Loc.T("Hotkeys.Duplicate"));

        var previousCreate = appliedCreate;
        var previousToggle = appliedToggle;
        UnregisterAll();

        var failures = new List<string>();
        if (!TryRegister(IdCreate, createChord, Loc.T("Hotkeys.CreateAction"), failures))
        {
            Restore(previousCreate, previousToggle);
            return HotkeyApplyResult.Failed(failures.ToArray());
        }
        if (!TryRegister(IdToggle, toggleChord, Loc.T("Hotkeys.ToggleAllAction"), failures))
        {
            UnregisterAll();
            Restore(previousCreate, previousToggle);
            return HotkeyApplyResult.Failed(failures.ToArray());
        }
        appliedCreate = createChord;
        appliedToggle = toggleChord;
        return HotkeyApplyResult.Success();
    }
    void Restore(string? createChord, string? toggleChord)
    {
        UnregisterAll();
        var ignored = new List<string>();
        TryRegister(IdCreate, createChord, Loc.T("Hotkeys.CreateAction"), ignored);
        TryRegister(IdToggle, toggleChord, Loc.T("Hotkeys.ToggleAllAction"), ignored);
        appliedCreate = NormalizeStored(createChord);
        appliedToggle = NormalizeStored(toggleChord);
    }
    bool TryRegister(int id, string? chord, string actionLabel, List<string> failures)
    {
        if (HotkeyChord.IsUnset(chord)) return true;
        if (!HotkeyChord.TryParse(chord, out uint mods, out uint vk, out string? parseError))
        {
            failures.Add($"{actionLabel}：{parseError}");
            return false;
        }
        if (RegisterHotKey(source.Handle, id, mods, vk))
        {
            registered.Add(id);
            return true;
        }
        failures.Add(Loc.F("Hotkeys.RegisterFailed", ("chord", chord!), ("actionLabel", actionLabel)));
        return false;
    }
    static string? NormalizeStored(string? chord) => HotkeyChord.IsUnset(chord) ? null : chord!.Trim();
    void UnregisterAll()
    {
        foreach (int id in registered.ToArray()) UnregisterHotKey(source.Handle, id);
        registered.Clear();
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (msg == 0x0312)
        {
            int id = wp.ToInt32();
            if (id == IdCreate) create();
            else if (id == IdToggle) toggle();
            handled = true;
        }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        UnregisterAll();
        source.RemoveHook(Hook);
        source.Dispose();
    }
}

public sealed class Tray : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Icon drawing;
    private readonly Forms.ToolStripMenuItem createItem;
    private readonly Forms.ToolStripMenuItem showItem;
    private readonly Forms.ToolStripMenuItem hideItem;
    private readonly Forms.ToolStripMenuItem indexItem;
    private readonly Forms.ToolStripMenuItem quitItem;
    private string? createChord;
    public Tray(NoteManager manager, Action openIndex, Action exit)
    {
        drawing = AppIcon.ForTray;
        var menu = new Forms.ContextMenuStrip();
        createItem = new Forms.ToolStripMenuItem("", null, (_, _) => manager.Create());
        showItem = new Forms.ToolStripMenuItem("", null, (_, _) => manager.ShowAll());
        hideItem = new Forms.ToolStripMenuItem("", null, (_, _) => manager.HideAll());
        indexItem = new Forms.ToolStripMenuItem("", null, (_, _) => openIndex());
        quitItem = new Forms.ToolStripMenuItem("", null, (_, _) => exit());
        menu.Items.Add(createItem);
        menu.Items.Add(showItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(indexItem);
        menu.Items.Add(quitItem);
        icon = new Forms.NotifyIcon { Icon = drawing, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => openIndex();
        ApplyLanguage();
        Loc.Changed += OnLanguageChanged;
    }
    void OnLanguageChanged() => ApplyLanguage();
    public void ApplyLanguage()
    {
        icon.Text = Loc.T("Tray.AppName");
        showItem.Text = Loc.T("Tray.ShowNotes");
        hideItem.Text = Loc.T("Notes.HideAll");
        indexItem.Text = Loc.T("Tray.OpenList");
        quitItem.Text = Loc.T("Tray.Quit");
        UpdateCreateShortcut(createChord);
    }
    public void UpdateCreateShortcut(string? chord)
    {
        createChord = chord;
        createItem.Text = HotkeyChord.IsUnset(chord)
            ? Loc.T("Hotkeys.CreateAction")
            : Loc.T("Hotkeys.CreateAction") + "    " + chord;
    }
    public void Notify(string message) { icon.BalloonTipTitle = Loc.BrandName; icon.BalloonTipText = message; icon.ShowBalloonTip(6000); }
    public void Dispose()
    {
        Loc.Changed -= OnLanguageChanged;
        icon.Visible = false; icon.ContextMenuStrip?.Dispose(); icon.Dispose(); drawing.Dispose();
    }
}

public sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle signal;
    private RegisteredWaitHandle? wait;
    public bool IsPrimary { get; }
    public SingleInstance(string dataDirectory)
    {
        string suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(dataDirectory).TrimEnd('\\').ToUpperInvariant())))[..24];
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PaperNotes.Activate." + suffix);
        mutex = new Mutex(true, "Local\\PaperNotes.Instance." + suffix, out bool created);
        IsPrimary = created;
        if (!created) signal.Set();
    }
    public void Listen(Dispatcher dispatcher, Action activate)
    { wait = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) => dispatcher.BeginInvoke(activate), null, Timeout.Infinite, false); }
    public void Dispose() { wait?.Unregister(null); if (IsPrimary) mutex.ReleaseMutex(); mutex.Dispose(); signal.Dispose(); }
}
