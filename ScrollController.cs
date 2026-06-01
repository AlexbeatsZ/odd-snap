using System.Runtime.InteropServices;

namespace LongSnapLite;

internal static class ScrollController
{
    private const int WheelDelta = 120;
    private const uint InputMouse = 0;
    private const uint MouseeventfWheel = 0x0800;

    public static void SendWheelDown(int notches)
    {
        if (notches <= 0)
        {
            return;
        }

        var input = new NativeMethods.INPUT
        {
            type = InputMouse,
            mi = new NativeMethods.MOUSEINPUT
            {
                mouseData = unchecked((uint)(-WheelDelta * notches)),
                dwFlags = MouseeventfWheel
            }
        };

        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    public static void SendWheelDownAt(Point screenPoint, int notches)
    {
        NativeMethods.SetCursorPos(screenPoint.X, screenPoint.Y);
        SendWheelDown(notches);
    }
}

internal static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int size);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetCursorPos(int x, int y);
}
