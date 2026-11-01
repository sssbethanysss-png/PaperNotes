using System.Windows.Input;
using PaperNotes;

static class HotkeyFieldTests
{
    public static void Run()
    {
        var field = new HotkeyField("新建便签", null);
        Tests.Check(field.DraftChord == null && HotkeyChord.Display(field.DraftChord) == Loc.T("Hotkeys.NotSet"), "hotkey row starts unset");

        field.BeginRecording();
        Tests.Check(HotkeyField.Active == field && field.IsRecording, "设置 enters recording");
        Tests.Check(field.TryCommitChord(ModifierKeys.None, Key.LeftCtrl) && field.IsRecording && field.DraftChord == null,
            "modifier-only keeps waiting");
        field.CancelFromEscape();
        Tests.Check(!field.IsRecording && field.DraftChord == null, "Esc cancels unset recording");

        field.BeginRecording();
        Tests.Check(field.TryCommitChord(ModifierKeys.Control | ModifierKeys.Shift, Key.N)
            && !field.IsRecording && field.DraftChord == "Ctrl+Shift+N", "commit Ctrl+Shift+N");

        field.BeginRecording();
        field.CancelFromEscape();
        Tests.Check(field.DraftChord == "Ctrl+Shift+N", "Esc restores value captured before this recording");

        field.ClearDraft();
        Tests.Check(field.DraftChord == null && HotkeyField.Active == null, "clear updates draft only");

        var a = new HotkeyField("A", null);
        var b = new HotkeyField("B", null);
        a.BeginRecording();
        b.BeginRecording();
        Tests.Check(HotkeyField.Active == b && !a.IsRecording, "starting another row cancels the previous recording");
        HotkeyField.CancelActiveRecording();

        using var keys = new Hotkeys(() => { }, () => { });
        var first = keys.Apply("Ctrl+Alt+Shift+F9", null);
        Tests.Check(first.Ok, "register uncommon chord for conflict test");
        using var conflict = new Hotkeys(() => { }, () => { });
        var second = conflict.Apply("Ctrl+Alt+Shift+F9", null);
        var expectedFail = Loc.F("Hotkeys.RegisterFailed", ("chord", "Ctrl+Alt+Shift+F9"), ("actionLabel", Loc.T("Hotkeys.CreateAction")));
        Tests.Check(!second.Ok && second.Failures.Count >= 1
            && second.Failures[0] == expectedFail, "conflict reports registration failure");
        Tests.Check(keys.Apply(null, null).Ok && keys.AppliedCreate == null, "save-clear unregisters after Apply(null)");

        // Cancel settings must not call Apply: draft clear without Apply leaves previous registration intact.
        using var live = new Hotkeys(() => { }, () => { });
        Tests.Check(live.Apply("Ctrl+Alt+Shift+F8", null).Ok, "seed live binding");
        var draft = new HotkeyField("live", live.AppliedCreate);
        draft.ClearDraft();
        Tests.Check(draft.DraftChord == null && live.AppliedCreate == "Ctrl+Alt+Shift+F8",
            "clear draft does not unregister until save");
        live.Apply(null, null);
    }
}
