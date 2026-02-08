using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

internal static class Program
{
    // Polling interval: low CPU, still feels instant.
    private const int PollMs = 150;

    // Prevents a feedback loop where we detect our own write as a "new copy".
    private static string? _lastSeen;
    private static string? _lastWritten;
    private static DateTime _lastWrittenAt = DateTime.MinValue;

    private static async Task Main()
    {
        IClipboardProvider clipboard = CreateProvider();

        Console.WriteLine("DBLT is watching your clipboard. Don't worry about it. 😉");
        Console.WriteLine("Press Ctrl+C to stop.");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var current = await clipboard.GetTextAsync();

                // Ignore empty or unchanged.
                if (string.IsNullOrEmpty(current) || current == _lastSeen)
                {
                    await Task.Delay(PollMs, cts.Token);
                    continue;
                }

                _lastSeen = current;

                // Ignore our own very recent write (debounce).
                // (Some systems re-emit the clipboard content after SetText.)
                if (_lastWritten != null &&
                    current == _lastWritten &&
                    (DateTime.UtcNow - _lastWrittenAt).TotalSeconds < 2.0)
                {
                    await Task.Delay(PollMs, cts.Token);
                    continue;
                }

                var cleaned = Normalize(current);

                // Only write if we actually changed something.
                if (!ReferenceEquals(cleaned, current) && cleaned != current)
                {
                    await clipboard.SetTextAsync(cleaned);
                    _lastWritten = cleaned;
                    _lastWrittenAt = DateTime.UtcNow;

                    Console.WriteLine("📋 Cleaned! Nobody will ever know.");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Clipboard can be temporarily locked/busy. Just retry.
            }

            try { await Task.Delay(PollMs, cts.Token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static IClipboardProvider CreateProvider()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsClipboardProvider();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return new LinuxClipboardProvider();

        throw new PlatformNotSupportedException(
            "ClipClean currently supports Windows and Linux only.");
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

