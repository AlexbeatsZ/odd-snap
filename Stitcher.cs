// Derived from OddSnap ScrollingCaptureForm.Capture.cs.
// Original project: https://github.com/jasperdevs/odd-snap
// License: GNU GPL v3. This LongSnapLite copy is modified for a minimal local tool.

using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LongSnapLite;

internal sealed class Stitcher : IDisposable
{
    private const double DuplicateThreshold = 0.985;
    private const int MinimumNewContentPixels = 1;
    private const int MinReliableMatchRows = 30;
    private const bool EnableBestGuessFallback = false;

    private Bitmap? _result;
    private Bitmap? _previousFrame;
    private int _bestMatchCount;
    private int _bestMatchIndex;
    private int _bestIgnoreBottomOffset;

    public int MaxOutputHeight { get; init; } = 50_000;
    public Bitmap? Result => _result;

    public AppendFrameResult AppendFrame(Bitmap current)
    {
        using var normalizedCurrent = Clone32Argb(current);

        if (_result is null)
        {
            _result = Clone32Argb(normalizedCurrent);
            ReplacePreviousCapturedFrame(normalizedCurrent);
            return AppendFrameResult.Appended(normalizedCurrent.Height, "First frame.");
        }

        if (_previousFrame is not null && AreFramesDuplicate(_previousFrame, normalizedCurrent))
        {
            ReplacePreviousCapturedFrame(normalizedCurrent);
            return AppendFrameResult.Duplicate("Frame is nearly identical to the previous capture.");
        }

        var match = TryAppendScrollingFrame(
            _result,
            normalizedCurrent,
            _bestMatchCount,
            _bestMatchIndex,
            _bestIgnoreBottomOffset,
            MaxOutputHeight);

        if (!match.Success)
        {
            return AppendFrameResult.NoReliableOverlap("Reliable overlap was not found.");
        }

        if (match.MaxHeightReached && match.Image is null)
        {
            return AppendFrameResult.MaxHeightReached(0, "Maximum output height reached.");
        }

        if (match.NewContentHeight < MinimumNewContentPixels)
        {
            ReplacePreviousCapturedFrame(normalizedCurrent);
            return AppendFrameResult.NoNewRows("Overlap was found, but there are no new rows.");
        }

        if (match.Image is null)
        {
            return match.MaxHeightReached
                ? AppendFrameResult.MaxHeightReached(0, "Maximum output height reached.")
                : AppendFrameResult.Error("Stitching produced no output image.");
        }

        if (!match.UsedBestGuess)
        {
            _bestMatchCount = Math.Max(_bestMatchCount, match.MatchCount);
            _bestMatchIndex = match.MatchIndex;
            _bestIgnoreBottomOffset = match.IgnoreBottomOffset;
        }

        _result.Dispose();
        _result = match.Image;
        ReplacePreviousCapturedFrame(normalizedCurrent);

        if (match.MaxHeightReached)
        {
            return AppendFrameResult.MaxHeightReached(match.NewContentHeight, "Maximum output height reached.");
        }

        return AppendFrameResult.Appended(
            match.NewContentHeight,
            match.UsedBestGuess ? "Best guess overlap." : $"Overlap {match.MatchCount} rows.");
    }

    private void ReplacePreviousCapturedFrame(Bitmap frame)
    {
        _previousFrame?.Dispose();
        _previousFrame = Clone32Argb(frame);
    }

    private readonly record struct ScrollAppendMatch(
        bool Success,
        Bitmap? Image,
        int NewContentHeight,
        int MatchCount,
        int MatchIndex,
        int IgnoreBottomOffset,
        bool UsedBestGuess,
        bool MaxHeightReached);

    private static ScrollAppendMatch TryAppendScrollingFrame(
        Bitmap result,
        Bitmap currentImage,
        int bestMatchCount,
        int bestMatchIndex,
        int bestIgnoreBottomOffset,
        int maxOutputHeight)
    {
        var match = TryFindScrollingAppend(result, currentImage, bestMatchCount, bestMatchIndex, bestIgnoreBottomOffset);
        if (!match.Success)
        {
            return match;
        }

        var keepResultHeight = result.Height - match.IgnoreBottomOffset;
        var totalHeight = Math.Min(keepResultHeight + match.NewContentHeight, maxOutputHeight);
        if (totalHeight <= keepResultHeight)
        {
            return match with { NewContentHeight = 0, MaxHeightReached = true };
        }

        var newResult = new Bitmap(result.Width, totalHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(newResult);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.None;

        g.DrawImage(
            result,
            new Rectangle(0, 0, result.Width, keepResultHeight),
            new Rectangle(0, 0, result.Width, keepResultHeight),
            GraphicsUnit.Pixel);

        var drawHeight = totalHeight - keepResultHeight;
        g.DrawImage(
            currentImage,
            new Rectangle(0, keepResultHeight, currentImage.Width, drawHeight),
            new Rectangle(0, match.MatchIndex + 1, currentImage.Width, drawHeight),
            GraphicsUnit.Pixel);

        return match with { Image = newResult, MaxHeightReached = totalHeight >= maxOutputHeight };
    }

    private static ScrollAppendMatch TryFindScrollingAppend(
        Bitmap result,
        Bitmap currentImage,
        int bestMatchCount,
        int bestMatchIndex,
        int bestIgnoreBottomOffset)
    {
        if (result.Width != currentImage.Width || result.Height <= 0 || currentImage.Height <= 0)
        {
            return new ScrollAppendMatch(false, null, 0, 0, 0, 0, false, false);
        }

        var matchCount = 0;
        var matchIndex = 0;
        var matchLimit = Math.Max(1, currentImage.Height / 2);
        var minReliableMatchRows = Math.Min(MinReliableMatchRows, Math.Max(1, currentImage.Height / 8));
        var ignoreSideOffset = Math.Max(50, currentImage.Width / 20);
        ignoreSideOffset = Math.Min(ignoreSideOffset, currentImage.Width / 3);
        var compareWidth = currentImage.Width - ignoreSideOffset * 2;
        if (compareWidth <= 0)
        {
            ignoreSideOffset = 0;
            compareWidth = currentImage.Width;
        }

        var ignoreBottomOffsetMax = Math.Max(0, currentImage.Height / 3);
        var ignoreBottomOffset = 0;

        var resultData = result.LockBits(
            new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        var currentData = currentImage.LockBits(
            new Rectangle(0, 0, currentImage.Width, currentImage.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var resultRowHashes = BuildRowHashes(resultData, ignoreSideOffset, compareWidth);
            var currentRowHashes = BuildRowHashes(currentData, ignoreSideOffset, compareWidth);

            if (ignoreBottomOffsetMax > 0)
            {
                var resultLastY = result.Height - 1;
                var currentLastY = currentImage.Height - 1;
                for (var offset = 0; offset <= ignoreBottomOffsetMax; offset++)
                {
                    if (!RowsEqual(
                            resultData,
                            currentData,
                            resultRowHashes,
                            currentRowHashes,
                            resultLastY - offset,
                            currentLastY - offset,
                            ignoreSideOffset,
                            compareWidth))
                    {
                        ignoreBottomOffset = offset;
                        break;
                    }
                }

                ignoreBottomOffset = Math.Max(ignoreBottomOffset, bestIgnoreBottomOffset);
                ignoreBottomOffset = Math.Min(ignoreBottomOffset, ignoreBottomOffsetMax);
            }

            var resultBottomY = result.Height - ignoreBottomOffset - 1;
            if (resultBottomY < 0)
            {
                return new ScrollAppendMatch(false, null, 0, 0, 0, ignoreBottomOffset, false, false);
            }

            for (var currentY = currentImage.Height - 1; currentY >= 0 && matchCount < matchLimit; currentY--)
            {
                var currentMatchCount = 0;
                for (var row = 0; currentY - row >= 0 && resultBottomY - row >= 0 && currentMatchCount < matchLimit; row++)
                {
                    if (!RowsEqual(
                            resultData,
                            currentData,
                            resultRowHashes,
                            currentRowHashes,
                            resultBottomY - row,
                            currentY - row,
                            ignoreSideOffset,
                            compareWidth))
                    {
                        break;
                    }

                    currentMatchCount++;
                }

                if (currentMatchCount > matchCount)
                {
                    matchCount = currentMatchCount;
                    matchIndex = currentY;
                }
            }
        }
        finally
        {
            result.UnlockBits(resultData);
            currentImage.UnlockBits(currentData);
        }

        var usedBestGuess = false;
        if (EnableBestGuessFallback && matchCount == 0 && bestMatchCount >= minReliableMatchRows)
        {
            matchCount = bestMatchCount;
            matchIndex = bestMatchIndex;
            ignoreBottomOffset = bestIgnoreBottomOffset;
            usedBestGuess = true;
        }

        if (matchCount < minReliableMatchRows)
        {
            return new ScrollAppendMatch(false, null, 0, matchCount, matchIndex, ignoreBottomOffset, usedBestGuess, false);
        }

        var newContentHeight = currentImage.Height - matchIndex - 1;
        if (newContentHeight <= 0)
        {
            return new ScrollAppendMatch(true, null, 0, matchCount, matchIndex, ignoreBottomOffset, usedBestGuess, false);
        }

        return new ScrollAppendMatch(true, null, newContentHeight, matchCount, matchIndex, ignoreBottomOffset, usedBestGuess, false);
    }

    private static unsafe bool RowsEqual(BitmapData aData, BitmapData bData, int aY, int bY, int x, int width)
    {
        if (aY < 0 || aY >= aData.Height || bY < 0 || bY >= bData.Height || width <= 0)
        {
            return false;
        }

        var a = (byte*)aData.Scan0 + aY * aData.Stride + x * 4;
        var b = (byte*)bData.Scan0 + bY * bData.Stride + x * 4;
        var bytes = width * 4;
        for (var i = 0; i < bytes; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool RowsEqual(
        BitmapData aData,
        BitmapData bData,
        ulong[] aHashes,
        ulong[] bHashes,
        int aY,
        int bY,
        int x,
        int width)
    {
        if (aY < 0 || aY >= aHashes.Length || bY < 0 || bY >= bHashes.Length)
        {
            return false;
        }

        return aHashes[aY] == bHashes[bY] && RowsEqual(aData, bData, aY, bY, x, width);
    }

    private static unsafe ulong[] BuildRowHashes(BitmapData data, int x, int width)
    {
        var hashes = new ulong[data.Height];
        if (width <= 0)
        {
            return hashes;
        }

        var byteOffset = x * 4;
        var byteWidth = width * 4;
        for (var y = 0; y < data.Height; y++)
        {
            hashes[y] = HashRow((byte*)data.Scan0 + y * data.Stride + byteOffset, byteWidth);
        }

        return hashes;
    }

    private static unsafe ulong HashRow(byte* row, int byteWidth)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;

        for (var i = 0; i < byteWidth; i++)
        {
            hash ^= row[i];
            hash *= prime;
        }

        return hash;
    }

    private static unsafe double CompareRegions(BitmapData prevData, BitmapData currData, int width, int prevY, int currY, int height)
    {
        if (height <= 0)
        {
            return 0;
        }

        var matches = 0;
        var total = 0;
        var rowStep = Math.Max(1, height / 24);
        var step = Math.Max(4, width / 64);

        for (var row = 0; row < height; row += rowStep)
        {
            var py = prevY + row;
            var cy = currY + row;
            if (py < 0 || py >= prevData.Height || cy < 0 || cy >= currData.Height)
            {
                continue;
            }

            var prevRow = (byte*)prevData.Scan0 + py * prevData.Stride;
            var currRow = (byte*)currData.Scan0 + cy * currData.Stride;

            for (var x = 0; x < width; x += step)
            {
                var off = x * 4;
                total++;
                var dr = prevRow[off + 2] - currRow[off + 2];
                var dg = prevRow[off + 1] - currRow[off + 1];
                var db = prevRow[off] - currRow[off];
                if (dr * dr + dg * dg + db * db < 100)
                {
                    matches++;
                }
            }
        }

        return total > 0 ? (double)matches / total : 0;
    }

    private static bool AreFramesDuplicate(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        var aData = a.LockBits(new Rectangle(0, 0, a.Width, a.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var bData = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            return CompareRegions(aData, bData, a.Width, 0, 0, a.Height) > DuplicateThreshold;
        }
        finally
        {
            a.UnlockBits(aData);
            b.UnlockBits(bData);
        }
    }

    private static Bitmap Clone32Argb(Bitmap source)
    {
        var clone = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(clone);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.DrawImageUnscaled(source, 0, 0);
        return clone;
    }

    public void Dispose()
    {
        _result?.Dispose();
        _previousFrame?.Dispose();
    }
}

internal readonly record struct AppendFrameResult(AppendFrameStatus Status, int RowsAppended, string Message)
{
    public static AppendFrameResult Appended(int rows, string message) => new(AppendFrameStatus.Appended, rows, message);

    public static AppendFrameResult Duplicate(string message) => new(AppendFrameStatus.Duplicate, 0, message);

    public static AppendFrameResult NoReliableOverlap(string message) => new(AppendFrameStatus.NoReliableOverlap, 0, message);

    public static AppendFrameResult NoNewRows(string message) => new(AppendFrameStatus.NoNewRows, 0, message);

    public static AppendFrameResult MaxHeightReached(int rows, string message) => new(AppendFrameStatus.MaxHeightReached, rows, message);

    public static AppendFrameResult Error(string message) => new(AppendFrameStatus.Error, 0, message);
}

internal enum AppendFrameStatus
{
    Appended,
    Duplicate,
    NoReliableOverlap,
    NoNewRows,
    MaxHeightReached,
    Error
}
