using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LongSnapLite;

internal sealed class HotkeyMessageWindow : NativeWindow, IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4C51;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkL = 0x4C;

    private bool _registered;

    public event EventHandler? HotkeyPressed;

    public HotkeyMessageWindow()
    {
        CreateHandle(new CreateParams());
    }

    public void Register()
    {
        if (_registered)
        {
            return;
        }

        if (!NativeMethods.RegisterHotKey(Handle, HotkeyId, ModWin | ModShift | ModNoRepeat, VkL))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Failed to register Win+Shift+L. Another app may already be using this hotkey.");
        }

        _registered = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _registered = false;
        }

        DestroyHandle();
    }
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
