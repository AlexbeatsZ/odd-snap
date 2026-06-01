using System.Runtime.InteropServices;

namespace LongSnapLite;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.Run(new LongSnapApplicationContext());
    }
}

internal sealed class LongSnapApplicationContext : ApplicationContext
{
    private readonly HotkeyMessageWindow _hotkeyWindow;
    private readonly LongCaptureService _captureService;
    private readonly NotifyIcon _trayIcon;

    public LongSnapApplicationContext()
    {
        _captureService = new LongCaptureService();
        _hotkeyWindow = new HotkeyMessageWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;
        _hotkeyWindow.Register();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "LongSnapLite",
            ContextMenuStrip = menu,
            Visible = true
        };
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        await _captureService.StartCaptureAsync();
    }

    protected override void ExitThreadCore()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _hotkeyWindow.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyWindow.Dispose();
        base.ExitThreadCore();
    }
}

internal static partial class NativeMethods
{
    internal static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
}
