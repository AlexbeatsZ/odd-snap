// Derived from OddSnap CaptureWindowExclusion.cs.
// Original project: https://github.com/jasperdevs/odd-snap
// License: GNU GPL v3. This LongSnapLite copy is modified for a minimal local tool.

using System.Runtime.InteropServices;

namespace LongSnapLite;

internal static class CaptureWindowExclusion
{
    private readonly record struct HiddenWindow(IntPtr Handle, bool WasTopmost);
    private sealed record RegisteredWindow(IntPtr Handle, Func<Rectangle>? BoundsProvider);

    private static readonly object Sync = new();
    private static readonly List<RegisteredWindow> RegisteredWindows = [];

    public static void Apply(Form form)
    {
        if (form.IsDisposed || !form.IsHandleCreated)
        {
            return;
        }

        Apply(form.Handle);
    }

    public static void Apply(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        }
        catch
        {
            // Best effort. Older Windows builds or unusual window styles can reject it.
        }

        Register(handle);
    }

    public static void Unregister(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        lock (Sync)
        {
            RegisteredWindows.RemoveAll(window => window.Handle == handle);
        }
    }

    public static T RunWithoutIntersectingWindows<T>(Rectangle captureRegion, Func<T> capture)
    {
        var hiddenHandles = HideIntersectingWindows(captureRegion);
        try
        {
            return capture();
        }
        finally
        {
            RestoreWindows(hiddenHandles);
        }
    }

    private static void Register(IntPtr handle)
    {
        lock (Sync)
        {
            PruneDeadHandles();
            if (!RegisteredWindows.Any(window => window.Handle == handle))
            {
                RegisteredWindows.Add(new RegisteredWindow(handle, null));
            }
        }
    }

    private static List<HiddenWindow> HideIntersectingWindows(Rectangle captureRegion)
    {
        List<RegisteredWindow> windows;
        lock (Sync)
        {
            PruneDeadHandles();
            windows = RegisteredWindows.ToList();
        }

        var hiddenHandles = new List<HiddenWindow>();
        foreach (var window in windows)
        {
            if (!ShouldHide(window, captureRegion))
            {
                continue;
            }

            var handle = window.Handle;
            var wasTopmost = (NativeMethods.GetWindowLong(handle, NativeMethods.GWL_EXSTYLE) & NativeMethods.WS_EX_TOPMOST) != 0;
            if (NativeMethods.ShowWindow(handle, NativeMethods.SW_HIDE))
            {
                hiddenHandles.Add(new HiddenWindow(handle, wasTopmost));
            }
        }

        if (hiddenHandles.Count > 0)
        {
            Thread.Sleep(16);
        }

        return hiddenHandles;
    }

    private static bool ShouldHide(RegisteredWindow window, Rectangle captureRegion)
    {
        var handle = window.Handle;
        if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle) || !NativeMethods.IsWindowVisible(handle))
        {
            return false;
        }

        Rectangle bounds;
        if (window.BoundsProvider is not null)
        {
            try
            {
                bounds = window.BoundsProvider();
            }
            catch
            {
                bounds = Rectangle.Empty;
            }
        }
        else
        {
            if (!NativeMethods.GetWindowRect(handle, out var rect))
            {
                return false;
            }

            bounds = rect.ToRectangle();
        }

        return bounds.Width > 0 && bounds.Height > 0 && captureRegion.IntersectsWith(bounds);
    }

    private static void RestoreWindows(List<HiddenWindow> windows)
    {
        foreach (var window in windows)
        {
            var handle = window.Handle;
            if (handle == IntPtr.Zero || !NativeMethods.IsWindow(handle))
            {
                continue;
            }

            NativeMethods.ShowWindow(handle, NativeMethods.SW_SHOWNOACTIVATE);
            if (window.WasTopmost)
            {
                NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }
    }

    private static void PruneDeadHandles()
    {
        RegisteredWindows.RemoveAll(window => window.Handle == IntPtr.Zero || !NativeMethods.IsWindow(window.Handle));
    }
}

internal static partial class NativeMethods
{
    internal const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
    internal const int GWL_EXSTYLE = -20;
    internal const int WS_EX_TOPMOST = 0x00000008;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNOACTIVATE = 4;
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal static readonly IntPtr HWND_TOPMOST = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    internal static extern nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    internal static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    internal static nint GetWindowLong(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
