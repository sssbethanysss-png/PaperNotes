using System;
using System.IO;
using System.Windows;

namespace PaperNotes;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { MessageBox.Show(Loc.T("App.DataUnavailablePrefix") + "\n\n" + ex.Message, Loc.T("App.StartFailedTitle")); }
    }
    private static void Run(string[] args)
    {
        Loc.EnsureLoaded();
        string dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PaperNotes");
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--data-dir") dataRoot = Path.GetFullPath(args[++i]);
        using var instance = new SingleInstance(dataRoot);
        if (!instance.IsPrimary) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var store = new NoteStore(Path.Combine(dataRoot, "notes"));
        var settingsStore = new SettingsStore(dataRoot); var settings = settingsStore.Load();
        Loc.SetLanguage(settings.UiLanguage, notify: false);
        var manager = new NoteManager(store, settings);
        app.DispatcherUnhandledException += (_, e) =>
        {
            if (e.Exception is IOException or UnauthorizedAccessException)
            { e.Handled = true; MessageBox.Show(Loc.T("Error.OperationFailedPrefix") + "\n" + e.Exception.Message, Loc.BrandName); }
        };
        var index = new IndexWindow(manager); manager.IndexRequested = index.Reveal;
        void Exit()
        {
            if (!manager.CloseAll()) { MessageBox.Show(Loc.T("App.ExitBlocked")); return; }
            index.AllowClose = true; index.Close(); app.Shutdown();
        }
        using var tray = new Tray(manager, index.Reveal, Exit);
        using var hotkeys = new Hotkeys(() => manager.Create(), manager.ToggleAll);
        // Missing Hotkey* fields mean never configured (older builds did not persist chords). Do not invent defaults.
        var startupBindings = hotkeys.Apply(settings.HotkeyCreate, settings.HotkeyToggleAll);
        tray.UpdateCreateShortcut(settings.HotkeyCreate);
        manager.VisibilityChanged += () =>
        {
            try { settingsStore.Save(settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { tray.Notify(Loc.T("Notes.VisibilitySaveFailedPrefix") + ex.Message); }
        };
        manager.PreferencesChanged += () =>
        {
            try { settingsStore.Save(settings); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { tray.Notify(Loc.T("Categories.SaveFailedPrefix") + ex.Message); }
        };
        index.SettingsRequested = () => new SettingsWindow(settings, settingsStore, hotkeys, () => tray.UpdateCreateShortcut(settings.HotkeyCreate))
            { Owner = index }.ShowDialog();
        instance.Listen(app.Dispatcher, index.Reveal);
        app.SessionEnding += (_, e) =>
        {
            // A Windows sign-out is an application exit, not a request to hide each note.
            if (!manager.CloseAll()) e.Cancel = true;
            else index.AllowClose = true;
        };
        if (!startupBindings.Ok && startupBindings.Failures.Count > 0)
            tray.Notify(string.Join("\n", startupBindings.Failures));
        if (store.Warnings.Count > 0) MessageBox.Show(string.Join("\n", store.Warnings), Loc.T("Recovery.Title"));
        if (settingsStore.RecoveredFromBackup) tray.Notify(Loc.T("Settings.RecoveryWarning"));
        manager.Restore(); if (Array.IndexOf(args, "--startup") < 0) index.Show();
        EventHandler displayChanged = (_, _) => app.Dispatcher.BeginInvoke(new Action(manager.RecoverOffscreenWindows));
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += displayChanged;
        try { app.Run(); }
        finally { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= displayChanged; }
    }
}
