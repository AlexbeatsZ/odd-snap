namespace LongSnapLite;

internal sealed class CaptureSessionOverlay : IDisposable
{
    private readonly BorderForm[] _borders;
    private readonly ToolbarForm _toolbar;
    private readonly TaskCompletionSource<CaptureSessionResult> _completion = new();

    private CaptureSessionOverlay(Rectangle selection)
    {
        _borders = CreateOutsideBorders(selection);

        _toolbar = new ToolbarForm(GetToolbarBounds(selection));
        _toolbar.Confirmed += (_, _) => _completion.TrySetResult(CaptureSessionResult.Confirm);
        _toolbar.Cancelled += (_, _) => _completion.TrySetResult(CaptureSessionResult.Cancel);
        _toolbar.FormClosed += (_, _) => _completion.TrySetResult(CaptureSessionResult.Cancel);
    }

    public Task<CaptureSessionResult> Completion => _completion.Task;

    public static CaptureSessionOverlay Show(Rectangle selection)
    {
        var overlay = new CaptureSessionOverlay(selection);
        foreach (var border in overlay._borders)
        {
            border.Show();
        }

        overlay._toolbar.Show();
        return overlay;
    }

    private static BorderForm[] CreateOutsideBorders(Rectangle selection)
    {
        var screen = SystemInformation.VirtualScreen;
        var borders = new List<BorderForm>(4);

        // Keep the guide outside the selected capture rectangle. If a border sits
        // inside the rectangle, CaptureWindowExclusion hides/restores it on every
        // capture tick, which produces the visible flashing the user reported.
        if (selection.Top > screen.Top)
        {
            borders.Add(new BorderForm(new Rectangle(selection.Left, selection.Top - 1, selection.Width, 1)));
        }

        if (selection.Bottom < screen.Bottom)
        {
            borders.Add(new BorderForm(new Rectangle(selection.Left, selection.Bottom, selection.Width, 1)));
        }

        if (selection.Left > screen.Left)
        {
            borders.Add(new BorderForm(new Rectangle(selection.Left - 1, selection.Top, 1, selection.Height)));
        }

        if (selection.Right < screen.Right)
        {
            borders.Add(new BorderForm(new Rectangle(selection.Right, selection.Top, 1, selection.Height)));
        }

        return borders.ToArray();
    }

    public void Cancel()
    {
        _completion.TrySetResult(CaptureSessionResult.Cancel);
    }

    public void Dispose()
    {
        foreach (var border in _borders)
        {
            border.Close();
            border.Dispose();
        }

        _toolbar.Close();
        _toolbar.Dispose();
    }

    private static Rectangle GetToolbarBounds(Rectangle selection)
    {
        const int width = 164;
        const int height = 40;
        var screen = SystemInformation.VirtualScreen;
        var x = Math.Clamp(selection.Right - width, screen.Left, screen.Right - width);
        var y = selection.Top - height - 8;

        if (y < screen.Top)
        {
            y = selection.Bottom + 8;
        }

        y = Math.Clamp(y, screen.Top, screen.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    private sealed class BorderForm : Form
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public BorderForm(Rectangle bounds)
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.White;
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CaptureWindowExclusion.Apply(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            CaptureWindowExclusion.Unregister(Handle);
            base.OnHandleDestroyed(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }

    private sealed class ToolbarForm : Form
    {
        public event EventHandler? Confirmed;
        public event EventHandler? Cancelled;

        public ToolbarForm(Rectangle bounds)
        {
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(35, 35, 35);
            Bounds = bounds;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;

            var confirm = new Button
            {
                Text = "Confirm",
                Bounds = new Rectangle(8, 7, 72, 26),
                FlatStyle = FlatStyle.System
            };
            var cancel = new Button
            {
                Text = "Cancel",
                Bounds = new Rectangle(86, 7, 70, 26),
                FlatStyle = FlatStyle.System
            };

            confirm.Click += (_, _) => Confirmed?.Invoke(this, EventArgs.Empty);
            cancel.Click += (_, _) => Cancelled?.Invoke(this, EventArgs.Empty);
            Controls.Add(confirm);
            Controls.Add(cancel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            CaptureWindowExclusion.Apply(this);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            CaptureWindowExclusion.Unregister(Handle);
            base.OnHandleDestroyed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Cancelled?.Invoke(this, EventArgs.Empty);
            }

            base.OnKeyDown(e);
        }
    }
}

internal enum CaptureSessionResult
{
    Confirm,
    Cancel
}
