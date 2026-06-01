namespace LongSnapLite;

internal static class GuidePainter
{
    public const int GuideThickness = 3;

    private const int DashLength = 8;
    private const int DashGap = 5;

    public static readonly Color GuideColor = Color.White;

    public static void DrawOutsideGuide(Graphics graphics, Rectangle selection)
    {
        using var brush = new SolidBrush(GuideColor);

        DrawHorizontalDashes(graphics, brush, selection.Left, selection.Right - 1, selection.Top - 1);
        DrawHorizontalDashes(graphics, brush, selection.Left, selection.Right - 1, selection.Bottom);
        DrawVerticalDashes(graphics, brush, selection.Left - 1, selection.Top, selection.Bottom - 1);
        DrawVerticalDashes(graphics, brush, selection.Right, selection.Top, selection.Bottom - 1);
    }

    public static Region BuildOutsideGuideRegion(Rectangle overlayBounds, Rectangle selection)
    {
        var region = new Region();
        region.MakeEmpty();

        AddIfValid(region, ToLocal(OutsideTop(overlayBounds, selection), overlayBounds));
        AddIfValid(region, ToLocal(OutsideBottom(overlayBounds, selection), overlayBounds));
        AddIfValid(region, ToLocal(OutsideLeft(overlayBounds, selection), overlayBounds));
        AddIfValid(region, ToLocal(OutsideRight(overlayBounds, selection), overlayBounds));

        return region;
    }

    private static void DrawHorizontalDashes(Graphics graphics, Brush brush, int left, int right, int y)
    {
        if (right < left || y < 0)
        {
            return;
        }

        for (var x = left; x <= right; x += DashLength + DashGap)
        {
            var width = Math.Min(DashLength, right - x + 1);
            graphics.FillRectangle(brush, x, y, width, 1);
        }
    }

    private static void DrawVerticalDashes(Graphics graphics, Brush brush, int x, int top, int bottom)
    {
        if (bottom < top || x < 0)
        {
            return;
        }

        for (var y = top; y <= bottom; y += DashLength + DashGap)
        {
            var height = Math.Min(DashLength, bottom - y + 1);
            graphics.FillRectangle(brush, x, y, 1, height);
        }
    }

    private static Rectangle OutsideTop(Rectangle screen, Rectangle selection)
    {
        var y = Math.Max(screen.Top, selection.Top - GuideThickness);
        var height = selection.Top - y;
        return new Rectangle(selection.Left, y, selection.Width, height);
    }

    private static Rectangle OutsideBottom(Rectangle screen, Rectangle selection)
    {
        var height = Math.Min(GuideThickness, screen.Bottom - selection.Bottom);
        return new Rectangle(selection.Left, selection.Bottom, selection.Width, height);
    }

    private static Rectangle OutsideLeft(Rectangle screen, Rectangle selection)
    {
        var x = Math.Max(screen.Left, selection.Left - GuideThickness);
        var width = selection.Left - x;
        return new Rectangle(x, selection.Top, width, selection.Height);
    }

    private static Rectangle OutsideRight(Rectangle screen, Rectangle selection)
    {
        var width = Math.Min(GuideThickness, screen.Right - selection.Right);
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
