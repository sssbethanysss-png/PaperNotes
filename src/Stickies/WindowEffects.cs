using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PaperNotes;

public static class WindowEffects
{
    private const int GwlStyle = -16;
    private const int WsMaximizeBox = 0x00010000;
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    public static void ApplyTopmost(Window window, bool value)
    {
        if (window.Topmost != value) window.Topmost = value;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !window.IsVisible || window.WindowState == WindowState.Minimized) return;
        bool nativeTopmost = (GetWindowLong(hwnd, -20) & 8) != 0;
        // Only flip the band when needed — re-applying HWND_TOPMOST on deactivate jumps above the note just clicked.
        if (nativeTopmost == value) return;
        SetWindowPos(hwnd, new IntPtr(value ? -1 : -2), 0, 0, 0, 0, 0x0213); // no move/size/activate/owner reorder
    }
    /// <summary>Raise within the current z-band (topmost stays above ordinary windows).</summary>
    public static void BringToFront(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero || !window.IsVisible || window.WindowState == WindowState.Minimized) return;
        // HWND_TOP keeps WS_EX_TOPMOST if set; otherwise stays below topmost windows.
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, 0x0213);
    }
    public static void SetNoteOpacity(Window window, bool translucent)
    {
        // WPF owns the layered HWND; do not change WS_EX_LAYERED behind HwndTarget's back.
        window.Opacity = translucent ? 0.85 : 1.0;
    }
    public static void DisableSnapMaximize(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        // Clear WS_MAXIMIZEBOX to reduce top-edge snap; NoteWindow also locks size in WINDOWPOSCHANGING.
        int style = GetWindowLong(hwnd, GwlStyle);
        SetWindowLong(hwnd, GwlStyle, style & ~WsMaximizeBox);
    }
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    public static void TryForeground(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        // SW_RESTORE(9) un-minimizes without forcing maximize (unlike SW_SHOWNOACTIVATE).
        if (window.WindowState == WindowState.Minimized) ShowWindow(hwnd, 9);
        SetForegroundWindow(hwnd);
    }
}
