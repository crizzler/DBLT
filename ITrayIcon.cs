using System;
using System.Threading;

/// <summary>
/// Abstraction over the platform-specific system-tray (notification area) icon.
/// </summary>
internal interface ITrayIcon : IDisposable
{
    /// <summary>Raised when the user clicks "Exit" in the context menu.</summary>
    event Action? ExitRequested;

    /// <summary>Raised when the user toggles the "Start with system" checkbox.</summary>
    event Action<bool>? AutoStartToggled;

    /// <summary>Set the initial checked state of the autostart menu item.</summary>
    void SetAutoStartChecked(bool isChecked);

    /// <summary>
    /// Show the tray icon and run the platform message loop.
    /// This method blocks until the icon is closed or <paramref name="ct"/> is cancelled.
    /// </summary>
    void Run(CancellationToken ct);
}
