using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

/// <summary>
/// Clipboard provider for Linux. Auto-detects Wayland vs X11 and picks
/// the right tool (wl-copy/wl-paste, xclip, or xsel).
/// </summary>
internal sealed class LinuxClipboardProvider : IClipboardProvider
{
    private readonly string  _readCmd;
    private readonly string[] _readArgs;
    private readonly string  _writeCmd;
    private readonly string[] _writeArgs;

    /// <summary>How long we wait for a clipboard tool before giving up.</summary>
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(2);

    public LinuxClipboardProvider()
    {
        (_readCmd, _readArgs, _writeCmd, _writeArgs) = ResolveTools();
        Console.WriteLine($"[DBLT] Using clipboard tool: {_readCmd}");
    }

    // ── Read ───────────────────────────────────────────────────────
    public async Task<string?> GetTextAsync()
    {
        var (exitCode, stdout) = await RunAsync(_readCmd, _readArgs);
        // wl-paste returns 1 when clipboard is empty; xclip returns 1 too.
        if (exitCode != 0 || stdout == null)
            return null;
        return stdout;
    }

    // ── Write ──────────────────────────────────────────────────────
    public async Task SetTextAsync(string text)
    {
        await RunWithStdinAsync(_writeCmd, _writeArgs, text);
    }

    // ── Tool resolution ────────────────────────────────────────────

    private static (string readCmd, string[] readArgs,
                     string writeCmd, string[] writeArgs) ResolveTools()
    {
        bool isWayland = IsWaylandSession();

        // 1. Prefer native Wayland tools when running under Wayland.
        if (isWayland && CommandExists("wl-paste") && CommandExists("wl-copy"))
        {
            return ("wl-paste",
                    new[] { "--no-newline", "-t", "text/plain" },
                    "wl-copy",
                    Array.Empty<string>());
        }

        // 2. xclip (works on X11, and on Wayland via XWayland).
        if (CommandExists("xclip"))
        {
            return ("xclip",
                    new[] { "-selection", "clipboard", "-o" },
                    "xclip",
                    new[] { "-selection", "clipboard", "-i" });
        }

        // 3. xsel as last resort.
        if (CommandExists("xsel"))
        {
            return ("xsel",
                    new[] { "--clipboard", "--output" },
                    "xsel",
                    new[] { "--clipboard", "--input" });
        }

        // Nothing found – give a helpful message.
        string hint = isWayland
            ? "Install wl-clipboard (e.g. sudo apt install wl-clipboard) for native Wayland support,\n" +
              "or install xclip/xsel for XWayland fallback."
            : "Install xclip (sudo apt install xclip) or xsel (sudo apt install xsel).";

        throw new InvalidOperationException(
            $"No supported clipboard tool found.\n{hint}");
    }

    private static bool IsWaylandSession()
    {
        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return true;
        return false;
    }

    private static bool CommandExists(string command)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName               = "which",
                Arguments              = command,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // ── Process helpers ────────────────────────────────────────────

    /// <summary>Run a command and capture its stdout.</summary>
    private static async Task<(int exitCode, string? stdout)> RunAsync(
        string fileName, string[] args)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName               = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            foreach (var a in args)
                proc.StartInfo.ArgumentList.Add(a);

            proc.Start();

            // Read stdout asynchronously to prevent deadlocks.
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            bool exited = proc.WaitForExit((int)ProcessTimeout.TotalMilliseconds);
            if (!exited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                return (-1, null);
            }

            string stdout = await stdoutTask;
            return (proc.ExitCode, stdout);
        }
        catch
        {
            return (-1, null);
        }
    }

    /// <summary>Run a command, piping <paramref name="stdin"/> to it.</summary>
    private static async Task RunWithStdinAsync(
        string fileName, string[] args, string stdin)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName               = fileName,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            foreach (var a in args)
                proc.StartInfo.ArgumentList.Add(a);

            proc.Start();

            await proc.StandardInput.WriteAsync(stdin);
            proc.StandardInput.Close();

            bool exited = proc.WaitForExit((int)ProcessTimeout.TotalMilliseconds);
            if (!exited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
            }
        }
        catch
        {
            // Swallow – the main loop will retry.
        }
    }
}
