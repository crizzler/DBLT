# Changelog

All notable changes to DBLT will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Changed
- Renamed project from ClipClean to **DBLT**
- App now runs as a **system tray icon** instead of a console window
  - Windows: native Win32 `Shell_NotifyIcon` (near the clock)
  - Linux: GTK3 + AppIndicator (libayatana-appindicator3)
  - Headless fallback if tray libraries aren't available
- Right-click tray menu with **"Start with Windows/system"** toggle
- Autostart support:
  - Windows: `HKCU\...\Run` registry key
  - Linux: `~/.config/autostart/dblt.desktop`

## [1.0.0] - 2026-02-08

### Added
- Initial release
- Clipboard monitoring with 150ms polling
- Replaces AI-typical Unicode characters with plain ASCII equivalents:
  - `—` (em dash) → `...`
  - `–` (en dash) → `...`
  - `'` `'` (smart quotes) → `'`
  - `"` `"` (curly quotes) → `"`
  - `…` (ellipsis) → `...`
- Cross-platform clipboard support:
  - Windows: Win32 P/Invoke (`user32.dll` / `kernel32.dll`)
  - Linux: auto-detects Wayland (`wl-clipboard`) or X11 (`xclip` / `xsel`)
- Self-contained single-file builds for Windows and Linux
- MIT License

[Unreleased]: https://github.com/crizzler/DBLT/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/crizzler/DBLT/releases/tag/v1.0.0
