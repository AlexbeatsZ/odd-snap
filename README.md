# LongSnapLite

LongSnapLite is a minimal Windows long screenshot tool. It runs in the background, listens for `Win + Shift + L`, lets you select a screen rectangle, continuously captures that rectangle while you scroll manually, stitches overlapping frames, and saves one PNG when you confirm.

It intentionally does not include OCR, annotation, upload, recording, history, settings, or an editor.

## Build

Install the .NET 8 SDK or newer, then run:

```powershell
cd C:\Users\Meta\Project\Scripts\AHK\LongSnapLite
dotnet build -c Debug -p:Platform=x64
dotnet build -c Release -p:Platform=x64
```

## Run

```powershell
dotnet run -c Release -p:Platform=x64
```

The app has no main window. It creates a tray icon named `LongSnapLite`.

## Capture

1. Focus the window or document you want to capture.
2. Press `Win + Shift + L`.
3. Drag a rectangle over the scrollable area.
4. Release the mouse button.
5. LongSnapLite shows a blue border around the selected area and a small `Confirm` / `Cancel` toolbar.
6. Scroll the underlying page yourself until enough content has been captured.
7. Click `Confirm` to save silently, or `Cancel` to discard.

Press `Esc` during selection or capture to cancel. The border does not intercept mouse wheel input, so the selected page remains scrollable.

## Output

Screenshots are saved to:

```text
C:\Users\Meta\Pictures\Screenshots
```

File name format:

```text
LongSnap_yyyyMMdd_HHmmss.png
```

## Exit

Right-click the tray icon and choose `Exit`. If the tray icon is not reachable, end the `LongSnapLite` process from Task Manager.

## Known Limitations

- This is desktop capture, not browser DOM full-page capture.
- It works best when the selected rectangle contains mostly vertically scrolling static content.
- Highly animated pages, sticky headers, video, cursor hover effects, or virtualized lists can reduce overlap reliability.
- If you scroll too far in one jump, there may not be enough overlap for stitching. Scroll in moderate increments.
- The stitcher stops appending when the output reaches `50000` pixels high, then waits for confirm/cancel.

## Third-Party Code

The scrolling stitcher and capture-window exclusion logic are derived from OddSnap:

```text
https://github.com/jasperdevs/odd-snap
```

OddSnap is licensed under GNU GPL v3. A copy of the license is included in `ODD-SNAP-GPL-3.0.txt`.
