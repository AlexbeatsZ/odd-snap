# OddSnap v0.8.41

## Changed
- default video recordings to 60 FPS and relabel recording quality as recording resolution.
- encode MP4/MKV/WebM recordings with sharper defaults while preserving native capture pixels unless a resolution cap is selected.
- keep recording controls visible in capture-excluded overlay windows instead of hiding them during full-screen recording.
- speed up window detection outlines and active-window capture by validating window bounds before full-screen bitmap capture.

## Fixed
- close the capture magnifier when switching tools, interacting with overlay controls, or leaving capture content.
- exclude OddSnap recording chrome from video and GIF frames without moving controls under the taskbar or off-screen.
- pad odd-sized video frames instead of downscaling them to encoder-safe even dimensions.
- harden capture, settings, upload, and background runtime paths against stale dispatchers, failed saves, and oversized HTTP responses.
