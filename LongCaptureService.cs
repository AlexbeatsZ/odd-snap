using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LongSnapLite;

internal sealed class LongCaptureService
{
    private const int MaxOutputHeight = 50_000;
    private const int CaptureIntervalMs = 450;

    private bool _isCapturing;

    public async Task StartCaptureAsync()
    {
        if (_isCapturing)
        {
            return;
        }

        _isCapturing = true;
        try
        {
            using var overlay = new SelectionOverlayForm();
            if (overlay.ShowDialog() != DialogResult.OK || overlay.SelectedRectangle is not { } selection)
            {
                return;
            }

            await Task.Delay(160);
            using var stitcher = new Stitcher { MaxOutputHeight = MaxOutputHeight };

            using (var first = ScreenCapture.CaptureRectangle(selection))
            {
                stitcher.AppendFrame(first);
            }

            using var sessionOverlay = CaptureSessionOverlay.Show(selection);
            var captureEnabled = true;
            while (!sessionOverlay.Completion.IsCompleted)
            {
                if (IsEscapePressed())
                {
                    sessionOverlay.Cancel();
                    break;
                }

                await Task.Delay(CaptureIntervalMs);
                if (sessionOverlay.Completion.IsCompleted || !captureEnabled)
                {
                    continue;
                }

                using var current = ScreenCapture.CaptureRectangle(selection);
                var appendResult = stitcher.AppendFrame(current);
                if (!appendResult.ShouldContinue || stitcher.Result?.Height >= MaxOutputHeight)
                {
                    captureEnabled = false;
                }
            }

            var decision = await sessionOverlay.Completion;

            if (decision != CaptureSessionResult.Confirm || stitcher.Result is null)
            {
                return;
            }

            SaveResult(stitcher.Result);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "LongSnapLite capture failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isCapturing = false;
        }
    }

    private static string SaveResult(Bitmap result)
    {
        var directory = @"C:\Users\Meta\Pictures\Screenshots";
        Directory.CreateDirectory(directory);
        var fileName = $"LongSnap_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var path = Path.Combine(directory, fileName);
        result.Save(path, ImageFormat.Png);
        return path;
    }

    private static bool IsEscapePressed()
    {
        return (NativeMethods.GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0;
    }

}

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);
}
