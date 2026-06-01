using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LongSnapLite;

internal static class ScreenCapture
{
    public static Bitmap CaptureRectangle(Rectangle rect)
    {
        return CaptureWindowExclusion.RunWithoutIntersectingWindows(rect, () => CaptureRectangleCore(rect));
    }

    private static Bitmap CaptureRectangleCore(Rectangle rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rect), "Capture rectangle must be non-empty.");
        }

        var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        var hdcDest = graphics.GetHdc();
        var hdcSrc = NativeMethods.GetDC(IntPtr.Zero);

        try
        {
            const int srccopy = 0x00CC0020;
            const int captureblt = 0x40000000;
            if (!NativeMethods.BitBlt(hdcDest, 0, 0, rect.Width, rect.Height, hdcSrc, rect.X, rect.Y, srccopy | captureblt))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "BitBlt capture failed.");
            }
        }
        finally
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, hdcSrc);
            graphics.ReleaseHdc(hdcDest);
        }

        return bitmap;
    }
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rop);
}
