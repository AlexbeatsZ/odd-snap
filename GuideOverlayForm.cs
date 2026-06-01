namespace LongSnapLite;

internal sealed class GuideOverlayForm : Form
{
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly Color TransparentColor = Color.Fuchsia;

    private readonly Rectangle _selection;

    public GuideOverlayForm(Rectangle selection)
    {
        _selection = selection;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = TransparentColor;
        Bounds = SystemInformation.VirtualScreen;
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        TransparencyKey = TransparentColor;

        Region = GuidePainter.BuildOutsideGuideRegion(Bounds, selection);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        NativeMethods.SetWindowPos(
            Handle,
            NativeMethods.HWND_TOPMOST,
            Bounds.X,
            Bounds.Y,
            Bounds.Width,
            Bounds.Height,
            NativeMethods.SWP_NOACTIVATE);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var localSelection = _selection;
        localSelection.Offset(-Bounds.X, -Bounds.Y);
        GuidePainter.DrawOutsideGuide(e.Graphics, localSelection);
    }
}
