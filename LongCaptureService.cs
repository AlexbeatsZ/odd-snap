using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace LongSnapLite;

internal sealed class LongCaptureService : IDisposable
{
    private const bool AutoScrollEnabled = false;
    private const bool CopyResultToClipboard = true;
    private const int AutoScrollNotches = 5;
    private const int MaxOutputHeight = 50_000;
    private const int CaptureIntervalMs = 400;
    private const int NoOverlapWarningThreshold = 6;
    private const int ClipboardRetryCount = 5;
    private const int ClipboardRetryDelayMs = 80;

    private readonly NotifyIcon _notifyIcon;
    private readonly object _sync = new();

    private CaptureState _state = CaptureState.Idle;
    private TaskCompletionSource<CaptureStopReason>? _stopRequest;
    private GuideOverlayForm? _guideOverlay;

    public LongCaptureService(NotifyIcon notifyIcon)
    {
        _notifyIcon = notifyIcon;
    }

    public Task ToggleCaptureAsync()
    {
        lock (_sync)
        {
            if (_state == CaptureState.Capturing && _stopRequest is not null)
            {
                _stopRequest.TrySetResult(CaptureStopReason.Save);
                return Task.CompletedTask;
            }

            if (_state != CaptureState.Idle)
            {
                return Task.CompletedTask;
            }

            _state = CaptureState.Selecting;
        }

        _ = RunCaptureAsync();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _stopRequest?.TrySetResult(CaptureStopReason.Cancel);
        CloseGuideOverlay();
    }

    private async Task RunCaptureAsync()
    {
        try
        {
            using var selectionOverlay = new SelectionOverlayForm();
            if (selectionOverlay.ShowDialog() != DialogResult.OK ||
                selectionOverlay.SelectedRectangle is not { } selection)
            {
                return;
            }

            await Task.Delay(120);

            using var stitcher = new Stitcher { MaxOutputHeight = MaxOutputHeight };
            using (var firstFrame = ScreenCapture.CaptureRectangle(selection))
            {
                stitcher.AppendFrame(firstFrame);
            }

            _guideOverlay = new GuideOverlayForm(selection);
            _guideOverlay.Show();

            var stopRequest = new TaskCompletionSource<CaptureStopReason>();
            lock (_sync)
            {
                _stopRequest = stopRequest;
                _state = CaptureState.Capturing;
            }

            var stopReason = await CaptureLoopAsync(selection, stitcher, stopRequest);
            CloseGuideOverlay();

            if (stopReason != CaptureStopReason.Save || stitcher.Result is null)
            {
                return;
            }

            SetState(CaptureState.Saving);
            var savedPath = SaveResult(stitcher.Result);
            var copiedToClipboard = CopyResultToClipboardIfEnabled(stitcher.Result);
            ShowSavedNotification(savedPath, copiedToClipboard);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "LongSnapLite capture failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            CloseGuideOverlay();
            lock (_sync)
            {
                _stopRequest = null;
                _state = CaptureState.Idle;
            }
        }
    }

    private async Task<CaptureStopReason> CaptureLoopAsync(
        Rectangle selection,
        Stitcher stitcher,
        TaskCompletionSource<CaptureStopReason> stopRequest)
    {
        var noReliableOverlapCount = 0;
        var warnedAboutOverlap = false;

        while (!stopRequest.Task.IsCompleted)
        {
            if (IsEscapePressed())
            {
                stopRequest.TrySetResult(CaptureStopReason.Cancel);
                break;
            }

            if (IsAutoScrollEnabled())
            {
                ScrollController.SendWheelDownAt(selection.Center(), AutoScrollNotches);
            }

            await Task.Delay(CaptureIntervalMs);

            if (stopRequest.Task.IsCompleted)
            {
                break;
            }

            using var currentFrame = ScreenCapture.CaptureRectangle(selection);
            var appendResult = stitcher.AppendFrame(currentFrame);

            switch (appendResult.Status)
            {
                case AppendFrameStatus.Appended:
                case AppendFrameStatus.NoNewRows:
                case AppendFrameStatus.Duplicate:
                    noReliableOverlapCount = 0;
                    break;
                case AppendFrameStatus.NoReliableOverlap:
                    noReliableOverlapCount++;
                    if (!warnedAboutOverlap && noReliableOverlapCount >= NoOverlapWarningThreshold)
                    {
                        warnedAboutOverlap = true;
                        ShowBalloonTip("LongSnapLite", "Stitching overlap is unreliable. Scroll back slightly or save the current result.");
                    }

                    break;
                case AppendFrameStatus.MaxHeightReached:
                    stopRequest.TrySetResult(CaptureStopReason.Save);
                    break;
                case AppendFrameStatus.Error:
                    ShowBalloonTip("LongSnapLite", appendResult.Message);
                    break;
            }

            if (stitcher.Result?.Height >= MaxOutputHeight)
            {
                stopRequest.TrySetResult(CaptureStopReason.Save);
            }
        }

        return await stopRequest.Task;
    }

    private void ShowSavedNotification(string savedPath, bool copiedToClipboard)
    {
        var message = copiedToClipboard ? $"{savedPath}{Environment.NewLine}Copied to clipboard." : savedPath;
        ShowBalloonTip("Long screenshot saved", message);
    }

    private void ShowBalloonTip(string title, string message)
    {
        if (_notifyIcon.Visible)
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }
    }

    private void SetState(CaptureState state)
    {
        lock (_sync)
        {
            _state = state;
        }
    }

    private void CloseGuideOverlay()
    {
        if (_guideOverlay is null)
        {
            return;
        }

        _guideOverlay.Close();
        _guideOverlay.Dispose();
        _guideOverlay = null;
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

    private bool CopyResultToClipboardIfEnabled(Bitmap result)
    {
        if (!IsCopyResultToClipboardEnabled())
        {
            return false;
        }

        try
        {
            CopyResultToClipboardWithRetry(result);
            return true;
        }
        catch (Exception ex)
        {
            ShowBalloonTip("LongSnapLite", $"Saved, but clipboard copy failed: {ex.Message}");
            return false;
        }
    }

    private static void CopyResultToClipboardWithRetry(Bitmap result)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < ClipboardRetryCount; attempt++)
        {
            try
            {
                using var clipboardBitmap = new Bitmap(result);
                Clipboard.SetImage(clipboardBitmap);
                return;
            }
            catch (ExternalException ex)
            {
                lastError = ex;
                Thread.Sleep(ClipboardRetryDelayMs);
            }
        }

        throw lastError ?? new InvalidOperationException("Clipboard is unavailable.");
    }

    private static bool IsEscapePressed()
    {
        return (NativeMethods.GetAsyncKeyState((int)Keys.Escape) & 0x8000) != 0;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool IsAutoScrollEnabled()
    {
        return AutoScrollEnabled;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool IsCopyResultToClipboardEnabled()
    {
        return CopyResultToClipboard;
    }
}

internal enum CaptureState
{
    Idle,
    Selecting,
    Capturing,
    Saving
}

internal enum CaptureStopReason
{
    Save,
    Cancel
}

internal static class RectangleExtensions
{
    public static Point Center(this Rectangle rectangle)
    {
        return new Point(rectangle.Left + rectangle.Width / 2, rectangle.Top + rectangle.Height / 2);
    }
}

internal static partial class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int virtualKey);
}
