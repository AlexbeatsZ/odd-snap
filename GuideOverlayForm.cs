using System.Drawing.Drawing2D;

namespace LongSnapLite;

internal sealed class GuideOverlayForm : Form
{
    private const int BorderThickness = 2;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private readonly Rectangle _selection;

    public GuideOverlayForm(Rectangle selection)
    {
        _selection = selection;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.White;
        Bounds = SystemInformation.VirtualScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        Region = BuildBorderRegion(Bounds, selection);
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

    private static Region BuildBorderRegion(Rectangle overlayBounds, Rectangle selection)
    {
        var region = new Region();
        region.MakeEmpty();

        AddIfValid(region, ToLocal(OutsideTop(overlayBounds, selection), overlayBounds));
        AddIfValid(region, ToLocal(OutsideBottom(overlayBounds, selection), overlayBounds));
        AddIfValid(region, ToLocal(OutsideLeft(overlayBounds, selection), overlayBounds));
        AddIfValid(region, ToLocal(OutsideRight(overlayBounds, selection), overlayBounds));

        return region;
    }

    private static Rectangle OutsideTop(Rectangle screen, Rectangle selection)
    {
        var y = Math.Max(screen.Top, selection.Top - BorderThickness);
        var height = selection.Top - y;
        return new Rectangle(selection.Left, y, selection.Width, height);
    }

    private static Rectangle OutsideBottom(Rectangle screen, Rectangle selection)
    {
        var height = Math.Min(BorderThickness, screen.Bottom - selection.Bottom);
        return new Rectangle(selection.Left, selection.Bottom, selection.Width, height);
    }

    private static Rectangle OutsideLeft(Rectangle screen, Rectangle selection)
    {
        var x = Math.Max(screen.Left, selection.Left - BorderThickness);
        var width = selection.Left - x;
        return new Rectangle(x, selection.Top, width, selection.Height);
    }

    private static Rectangle OutsideRight(Rectangle screen, Rectangle selection)
    {
        var width = Math.Min(BorderThickness, screen.Right - selection.Right);
        return new Rectangle(selection.Right, selection.Top, width, selection.Height);
    }

    private static Rectangle ToLocal(Rectangle rectangle, Rectangle overlayBounds)
    {
        rectangle.Offset(-overlayBounds.X, -overlayBounds.Y);
        return rectangle;
    }

    private static void AddIfValid(Region region, Rectangle rectangle)
    {
        if (rectangle.Width > 0 && rectangle.Height > 0)
        {
            region.Union(rectangle);
        }
    }
}
