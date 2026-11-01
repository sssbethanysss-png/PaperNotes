using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace PaperNotes;

public static class WindowPlacement
{
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    public static void Capture(Window window, Note note)
    {
        if (window.WindowState != WindowState.Normal || !window.IsLoaded) return;
        if (GetWindowRect(new WindowInteropHelper(window).Handle, out var rect)) { note.X = rect.Left; note.Y = rect.Top; }
        note.Width = window.ActualWidth; note.Height = window.ActualHeight;
    }
    public static System.Drawing.Rectangle Fit(System.Drawing.Rectangle bounds, System.Drawing.Rectangle area)
    {
        int w = Math.Min(bounds.Width, area.Width), h = Math.Min(bounds.Height, area.Height);
        return new System.Drawing.Rectangle(Math.Clamp(bounds.X, area.Left, area.Right - w), Math.Clamp(bounds.Y, area.Top, area.Bottom - h), w, h);
    }
    public static void Restore(Window window, Note note)
    {
        var handle = new WindowInteropHelper(window).Handle;
        // Move first so WPF sees the destination monitor DPI before sizing.
        SetWindowPos(handle, IntPtr.Zero, (int)note.X, (int)note.Y, 0, 0, 0x0015);
        window.UpdateLayout();
        GetWindowRect(handle, out var rect);
        var bounds = new System.Drawing.Rectangle((int)note.X, (int)note.Y, Math.Max(180, rect.Right - rect.Left), Math.Max((int)Note.MinimumHeight, rect.Bottom - rect.Top));
        var area = Forms.Screen.FromRectangle(bounds).WorkingArea;
        var fit = Fit(bounds, area);
        SetWindowPos(handle, IntPtr.Zero, fit.X, fit.Y, fit.Width, fit.Height, 0x0014);
    }
    public static void CenterAtMouse(Note note)
    {
        var area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
        note.X = area.Left + (area.Width - note.Width) / 2;
        note.Y = area.Top + (area.Height - note.Height) / 2;
    }
}
