# LongSnapLite

LongSnapLite is a minimal Windows long screenshot tool. It runs in the background and only keeps one feature: capturing a selected scrolling region into a PNG.

It does not include OCR, annotation, recording, upload, history, settings, or an editor.

## Build

Install the .NET 8 SDK or newer, then run:

```powershell
cd C:\Users\Meta\Project\Scripts\AHK\LongSnapLite
dotnet build -c Debug -p:Platform=x64
dotnet build -c Release -p:Platform=x64
```

## Publish

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:PublishTrimmed=false
```

## Run

```powershell
dotnet run -c Release -p:Platform=x64
```

The app has no main window. It creates a tray icon named `LongSnapLite`. Right-click the tray icon and choose `Exit` to quit.

## Capture

1. Start the app.
2. Press `Win + Shift + L`.
3. Drag a rectangle over the scrollable area.
4. Scroll the page manually.
5. Press `Win + Shift + L` again to save and copy the result to the clipboard.
6. Press `Esc` during selection or capture to cancel.

During capture, LongSnapLite shows only a thin white guide around the selected region. It does not show a Confirm/Cancel toolbar.

## Output

Screenshots are saved to:

```text
C:\Users\Meta\Pictures\Screenshots
```

File name format:

```text
LongSnap_yyyyMMdd_HHmmss.png
```

Successful saves use a tray notification. Save failures show an error dialog.

By default, the final stitched image is also copied to the Windows clipboard after it is saved. This is controlled by the `CopyResultToClipboard` constant in `LongCaptureService.cs`; there is no settings UI.

## Known Limitations

- This is desktop capture, not browser DOM full-page capture.
- It works best when the selected rectangle contains mostly vertical static content.
- Highly animated pages, sticky headers, video, cursor hover effects, or virtualized lists can reduce overlap reliability.
- Scroll in moderate increments. If you jump too far, there may not be enough overlap to stitch reliably.
- The stitcher stops when the output reaches `50000` pixels high and saves the current result.

## Third-Party Code

The scrolling stitcher and capture-window exclusion logic are derived from OddSnap:

```text
https://github.com/jasperdevs/odd-snap
```

OddSnap is licensed under GNU GPL v3. A copy of the license is included in `ODD-SNAP-GPL-3.0.txt`.
