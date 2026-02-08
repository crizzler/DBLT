# DBLT

**Don't get caught out there.** ✨

DBLT is a lightweight clipboard monitor that silently replaces those telltale AI-generated Unicode characters — em dashes, smart quotes, curly apostrophes — with their normal, human-typed equivalents before you paste.

You know those fancy `—` and `'` that no human has ever intentionally typed? Yeah, those. DBLT takes care of them so you don't embarrass yourself on social media.

Runs on **Windows** and **Linux** (Wayland + X11). Zero config. Just run it.

---

## ⬇️ Download for Windows

**No installation needed. No tech skills required. Just download, unzip, and run.**

### 👉 [Download DBLT for Windows (64-bit)](https://github.com/crizzler/DBLT/releases/latest/download/DBLT-v1.0.0-win-x64.zip)

1. Click the link above to download the `.zip` file
2. Right-click the downloaded file → **Extract All**
3. Double-click **`DBLT-v1.0.0-win-x64.exe`**
4. Done! DBLT runs in the background. Just copy and paste as usual.

> 💡 To stop it, click the terminal window and press `Ctrl+C`, or just close it.

---

## Why?

Because some people learned the hard way that posting AI-generated text with all its fancy Unicode fingerprints is... noticeable. 👀

DBLT makes sure your clipboard is clean before you hit paste. That's it. That's the app.

## What It Does

DBLT watches your clipboard and instantly swaps out AI-typical characters:

| Before | After |
|--------|-------|
| `—` (em dash) | `...` |
| `–` (en dash) | `...` |
| `'` `'` (smart quotes) | `'` |
| `"` `"` (curly double quotes) | `"` |
| `…` (ellipsis character) | `...` |

### Example

**You copy this (straight from the AI):**
> A Remember Prefab—class name SaveablePrefab—is Crystal Save's Swiss-army knife for prefab persistence. It's very reliable.

**DBLT fixes it to:**
> A Remember Prefab...class name SaveablePrefab...is Crystal Save's Swiss-army knife for prefab persistence. It's very reliable.

Now it looks like a human wrote it. You're welcome.

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

DBLT runs silently in the background, checking your clipboard every 150ms. Press **Ctrl+C** to stop.

Just leave it running. Copy text. Paste text. Nobody will ever know. 🤫

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

## What does DBLT stand for?

It stands for DBLT. That's it. Don't worry about it. 😉

## License

[MIT](LICENSE)
