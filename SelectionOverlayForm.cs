namespace LongSnapLite;

internal sealed class SelectionOverlayForm : Form
{
    private Point _startScreenPoint;
    private Point _currentScreenPoint;
    private bool _dragging;
    private Rectangle _lastSelectionRectangle;

    public Rectangle? SelectedRectangle { get; private set; }

    public SelectionOverlayForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        Bounds = SystemInformation.VirtualScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Color.Black;
        Opacity = 0.25;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        KeyPreview = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            SelectedRectangle = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _startScreenPoint = Cursor.Position;
        _currentScreenPoint = _startScreenPoint;
        _lastSelectionRectangle = Rectangle.Empty;
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var oldRect = ToLocalInflated(_lastSelectionRectangle);
        _currentScreenPoint = Cursor.Position;
        _lastSelectionRectangle = Normalize(_startScreenPoint, _currentScreenPoint);

        var newRect = ToLocalInflated(_lastSelectionRectangle);
        Invalidate(Rectangle.Union(oldRect, newRect));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_dragging || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        Capture = false;
        _currentScreenPoint = Cursor.Position;
        var rect = Normalize(_startScreenPoint, _currentScreenPoint);
        if (rect.Width < 8 || rect.Height < 8)
        {
            SelectedRectangle = null;
            DialogResult = DialogResult.Cancel;
        }
        else
        {
            SelectedRectangle = rect;
            DialogResult = DialogResult.OK;
        }

        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (!_dragging)
        {
            return;
        }

        var rect = Normalize(_startScreenPoint, _currentScreenPoint);
        rect.Offset(-Bounds.X, -Bounds.Y);
        GuidePainter.DrawOutsideGuide(e.Graphics, rect);
    }

    private static Rectangle Normalize(Point a, Point b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new Rectangle(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }

    private Rectangle ToLocalInflated(Rectangle rectangle)
    {
        if (rectangle.IsEmpty)
        {
            return Rectangle.Empty;
        }

        rectangle.Offset(-Bounds.X, -Bounds.Y);
        rectangle.Inflate(GuidePainter.GuideThickness + 1, GuidePainter.GuideThickness + 1);
        return rectangle;
    }
}
