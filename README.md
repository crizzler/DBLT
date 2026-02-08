# ClipClean

A lightweight clipboard monitor that automatically normalizes fancy Unicode characters into their plain ASCII equivalents. Runs on **Windows** and **Linux** (Wayland + X11).

## What It Does

When you copy text containing typographic characters like smart quotes, em dashes, or curly apostrophes, ClipClean instantly replaces them in your clipboard:

| Before | After |
|--------|-------|
| `—` (em dash) | `...` |
| `–` (en dash) | `...` |
| `'` `'` (smart quotes) | `'` |
| `"` `"` (curly double quotes) | `"` |
| `…` (ellipsis character) | `...` |

### Example

**Copied:**
> A Remember Prefab—class name SaveablePrefab—is Crystal Save's Swiss-army knife for prefab persistence. It's very reliable.

**Result in clipboard:**
> A Remember Prefab...class name SaveablePrefab...is Crystal Save's Swiss-army knife for prefab persistence. It's very reliable.

## Installation

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (to build from source)
- **Linux only:** a clipboard tool:
  - Wayland (GNOME, KDE 6, etc.): `sudo apt install wl-clipboard`
  - X11: `sudo apt install xclip` (or `xsel`)
- **Windows:** works out of the box, no extra dependencies

### Build from Source

```bash
# Clone
git clone https://github.com/crizzler/DBLT.git
cd DBLT

# Run directly
dotnet run

# Or publish a self-contained single-file binary
# Windows
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

# Linux
dotnet publish -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

Published binaries are in `bin/Release/net9.0/<rid>/publish/`.

### Pre-built Binaries

Check the [Releases](https://github.com/crizzler/DBLT/releases) page for ready-to-use executables.

## Usage

```bash
./ClipClean            # Linux
ClipClean.exe          # Windows
```

The app runs in the background, polling the clipboard every 150ms. Press **Ctrl+C** to stop.

## How It Works

- **Windows:** Native Win32 clipboard API via P/Invoke (`user32.dll` / `kernel32.dll`)
- **Linux:** Auto-detects the display server and uses the appropriate tool:
  1. **Wayland** → `wl-paste` / `wl-copy`
  2. **X11 / XWayland fallback** → `xclip`
  3. **Last resort** → `xsel`

## Project Structure

```
ClipClean.csproj                 # Project file (.NET 9)
Program.cs                       # Main loop + text normalization
IClipboardProvider.cs            # Clipboard abstraction interface
WindowsClipboardProvider.cs      # Win32 P/Invoke implementation
LinuxClipboardProvider.cs        # Linux CLI-tool implementation
```

## License

[MIT](LICENSE)
