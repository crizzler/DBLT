using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    private const int PollMs = 150;

    private static string? _lastSeen;
    private static string? _lastWritten;
    private static DateTime _lastWrittenAt = DateTime.MinValue;

    [STAThread]
    private static void Main()
    {
        // Hide console window on Windows — the tray icon is the UI.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            WindowsTrayIcon.HideConsoleWindow();

        var clipboard = CreateProvider();
        using var cts = new CancellationTokenSource();

        // Ctrl+C still works in headless / terminal mode.
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // Start clipboard monitoring on a background thread.
        _ = Task.Run(() => MonitorClipboard(clipboard, cts.Token));

        // Create and run the tray icon on the main thread (blocks).
        using var tray = CreateTrayIcon();

        tray.SetAutoStartChecked(AutoStartManager.IsEnabled());

        tray.AutoStartToggled += enabled =>
        {
            if (enabled) AutoStartManager.Enable();
            else         AutoStartManager.Disable();
        };

        tray.ExitRequested += () => cts.Cancel();

        tray.Run(cts.Token);   // blocks until exit

        cts.Cancel();          // ensure monitor task stops
    }

    // ── Clipboard monitoring loop ──────────────────────────────────
    private static async Task MonitorClipboard(
        IClipboardProvider clipboard, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var current = await clipboard.GetTextAsync();

                if (string.IsNullOrEmpty(current) || current == _lastSeen)
                {
                    await Task.Delay(PollMs, ct);
                    continue;
                }

                _lastSeen = current;

                if (_lastWritten != null &&
                    current == _lastWritten &&
                    (DateTime.UtcNow - _lastWrittenAt).TotalSeconds < 2.0)
                {
                    await Task.Delay(PollMs, ct);
                    continue;
                }

                var cleaned = Normalize(current);

                if (!ReferenceEquals(cleaned, current) && cleaned != current)
                {
                    await clipboard.SetTextAsync(cleaned);
                    _lastWritten = cleaned;
                    _lastWrittenAt = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* clipboard temporarily busy */ }

            try { await Task.Delay(PollMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Factories ──────────────────────────────────────────────────
    private static IClipboardProvider CreateProvider()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsClipboardProvider();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxClipboardProvider();
        throw new PlatformNotSupportedException(
            "DBLT currently supports Windows and Linux only.");
    }

    private static ITrayIcon CreateTrayIcon()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsTrayIcon();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (LinuxTrayIcon.IsAvailable())
                return new LinuxTrayIcon();

            Console.WriteLine("[DBLT] AppIndicator libraries not found — running without tray icon.");
            return new HeadlessTrayIcon();
        }

        return new HeadlessTrayIcon();
    }

    private static string Normalize(string s)
    {
        // Fast-path: if nothing to replace, return original string (avoids allocations).
        if (!NeedsNormalization(s))
            return s;

        // You can extend this mapping over time.
        return s
            // dashes
            .Replace("—", "...")   // em dash
            .Replace("–", "...")   // en dash (optional but usually desired)
            // apostrophes / quotes
            .Replace("’", "'")
            .Replace("‘", "'")
            .Replace("“", "\"")
            .Replace("”", "\"")
            // ellipsis character
            .Replace("…", "...");
    }

    private static bool NeedsNormalization(string s)
    {
        // Only trigger when the target chars exist.
        // Keeps the app from “touching” unrelated clipboard text.
        return s.IndexOf('—') >= 0 ||
               s.IndexOf('–') >= 0 ||
               s.IndexOf('’') >= 0 ||
               s.IndexOf('‘') >= 0 ||
               s.IndexOf('“') >= 0 ||
               s.IndexOf('”') >= 0 ||
               s.IndexOf('…') >= 0;
    }
}

