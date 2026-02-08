using System;
using System.Threading;

/// <summary>
/// Fallback "tray icon" that simply blocks until cancellation.
/// Used when native tray-icon libraries are not available.
/// </summary>
internal sealed class HeadlessTrayIcon : ITrayIcon
{
#pragma warning disable CS0067 // Events are required by ITrayIcon interface
    public event Action? ExitRequested;
    public event Action<bool>? AutoStartToggled;
#pragma warning restore CS0067

    public void SetAutoStartChecked(bool isChecked) { }

    public void Run(CancellationToken ct)
    {
        Console.WriteLine("DBLT is running in background mode (no tray icon).");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();
        Console.WriteLine("Tip: Install libayatana-appindicator3-1 for a tray icon:");
        Console.WriteLine("  sudo apt install libayatana-appindicator3-1 gir1.2-ayatanaappindicator3-0.1");

        ct.WaitHandle.WaitOne();
    }

    public void Dispose() { }
}
