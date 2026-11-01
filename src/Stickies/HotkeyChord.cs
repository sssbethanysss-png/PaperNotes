using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace PaperNotes;

/// <summary>Parse/format global hotkey chords stored as e.g. "Ctrl+Shift+N".</summary>
public static class HotkeyChord
{
    public static string UnsetDisplay => Loc.T("Hotkeys.NotSet");
    public static string RecordingHint => Loc.T("Hotkeys.Recording");

    public static bool IsUnset(string? chord) => string.IsNullOrWhiteSpace(chord);

    public static string Display(string? chord) => IsUnset(chord) ? UnsetDisplay : chord!.Trim();

    public static bool TryParse(string? chord, out uint modifiers, out uint virtualKey, out string? error)
    {
        modifiers = 0; virtualKey = 0; error = null;
        if (IsUnset(chord)) { error = Loc.T("Hotkeys.NotSet"); return false; }
        var parts = chord!.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) { error = Loc.T("Hotkeys.ModifierAndKeyRequired"); return false; }
        ModifierKeys mods = ModifierKeys.None;
        Key? key = null;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Control;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Shift;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Alt;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                mods |= ModifierKeys.Windows;
            else if (key != null) { error = Loc.T("Hotkeys.InvalidFormat"); return false; }
            else if (!TryParseKey(part, out var parsed)) { error = Loc.T("Hotkeys.UnknownKeyPrefix") + part; return false; }
            else key = parsed;
        }
        if (key == null) { error = Loc.T("Hotkeys.KeyRequired"); return false; }
        if (!IsValidGlobal(mods, key.Value, out error)) return false;
        modifiers = ToNativeModifiers(mods) | 0x4000; // MOD_NOREPEAT
        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key.Value);
        if (virtualKey == 0) { error = Loc.T("Hotkeys.UnknownKey"); return false; }
        return true;
    }

    public static string Format(ModifierKeys mods, Key key)
    {
        var parts = new List<string>();
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(FormatKey(key));
        return string.Join("+", parts);
    }

    public static bool IsValidGlobal(ModifierKeys mods, Key key, out string? error)
    {
        error = null;
        if (IsModifierKey(key)) { error = Loc.T("Hotkeys.ModifierOnly"); return false; }
        bool hasStrong = mods.HasFlag(ModifierKeys.Control) || mods.HasFlag(ModifierKeys.Alt) || mods.HasFlag(ModifierKeys.Windows);
        if (!hasStrong)
        {
            error = Loc.T("Hotkeys.RequiredModifier");
            return false;
        }
        if (mods == ModifierKeys.None)
        {
            error = Loc.T("Hotkeys.BareLetterOrDigit");
            return false;
        }
        return true;
    }

    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin
            or Key.System or Key.None;

    public static Key NormalizeKey(Key key, Key systemKey) =>
        key == Key.System ? systemKey : key;

    public static ModifierKeys CurrentModifiers()
    {
        ModifierKeys mods = ModifierKeys.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= ModifierKeys.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= ModifierKeys.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= ModifierKeys.Alt;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mods |= ModifierKeys.Windows;
        return mods;
    }

    static uint ToNativeModifiers(ModifierKeys mods)
    {
        uint native = 0;
        if (mods.HasFlag(ModifierKeys.Alt)) native |= 0x0001;
        if (mods.HasFlag(ModifierKeys.Control)) native |= 0x0002;
        if (mods.HasFlag(ModifierKeys.Shift)) native |= 0x0004;
        if (mods.HasFlag(ModifierKeys.Windows)) native |= 0x0008;
        return native;
    }

    static bool TryParseKey(string part, out Key key)
    {
        key = Key.None;
        if (part.Length == 1)
        {
            char ch = char.ToUpperInvariant(part[0]);
            if (ch is >= 'A' and <= 'Z') { key = Key.A + (ch - 'A'); return true; }
            if (ch is >= '0' and <= '9') { key = Key.D0 + (ch - '0'); return true; }
        }
        if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(part[1..], out int fn) && fn is >= 1 and <= 24)
        {
            key = Key.F1 + (fn - 1);
            return true;
        }
        var map = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = Key.Space, ["Tab"] = Key.Tab, ["Enter"] = Key.Enter, ["Return"] = Key.Return,
            ["Escape"] = Key.Escape, ["Esc"] = Key.Escape, ["Backspace"] = Key.Back, ["Back"] = Key.Back,
            ["Delete"] = Key.Delete, ["Del"] = Key.Delete, ["Insert"] = Key.Insert, ["Ins"] = Key.Insert,
            ["Home"] = Key.Home, ["End"] = Key.End, ["PageUp"] = Key.PageUp, ["PageDown"] = Key.PageDown,
            ["Up"] = Key.Up, ["Down"] = Key.Down, ["Left"] = Key.Left, ["Right"] = Key.Right,
            ["OemPlus"] = Key.OemPlus, ["OemMinus"] = Key.OemMinus, ["Plus"] = Key.OemPlus, ["Minus"] = Key.OemMinus,
        };
        if (map.TryGetValue(part, out key)) return true;
        return Enum.TryParse(part, true, out key) && key != Key.None;
    }

    static string FormatKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z) return ((char)('A' + (key - Key.A))).ToString();
        if (key is >= Key.D0 and <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();
        if (key is >= Key.F1 and <= Key.F24) return "F" + (1 + (key - Key.F1));
        return key switch
        {
            Key.Space => "Space",
            Key.Return or Key.Enter => "Enter",
            Key.Escape => "Esc",
            Key.Back => "Backspace",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            _ => key.ToString()
        };
    }
}

public readonly record struct HotkeyApplyResult(bool Ok, IReadOnlyList<string> Failures)
{
    public static HotkeyApplyResult Success() => new(true, Array.Empty<string>());
    public static HotkeyApplyResult Failed(params string[] failures) => new(false, failures);
}
