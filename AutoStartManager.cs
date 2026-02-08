using System;
using System.IO;
using System.Runtime.InteropServices;

/// <summary>
/// Manages "start on boot" registration on Windows (Registry) and Linux (.desktop file).
/// </summary>
internal static class AutoStartManager
{
    private const string AppName = "DBLT";

    // ── Public API ─────────────────────────────────────────────────
    public static bool IsEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return IsEnabledWindows();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return IsEnabledLinux();
        return false;
    }

    public static void Enable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            EnableWindows();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            EnableLinux();
    }

    public static void Disable()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            DisableWindows();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            DisableLinux();
    }

    // ════════════════════════════════════════════════════════════════
    //  Windows — HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
    // ════════════════════════════════════════════════════════════════

    private static readonly IntPtr HKEY_CURRENT_USER = (IntPtr)unchecked((int)0x80000001);
    private const uint KEY_READ  = 0x20019;
    private const uint KEY_WRITE = 0x20006;
    private const uint REG_SZ    = 1;
    private const int  ERROR_SUCCESS = 0;
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKeyExW(
        IntPtr hKey, string subKey, uint options, uint samDesired, out IntPtr phkResult);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegSetValueExW(
        IntPtr hKey, string valueName, int reserved, uint dwType,
        string lpData, int cbData);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegQueryValueExW(
        IntPtr hKey, string valueName, IntPtr reserved,
        out uint lpType, IntPtr lpData, ref int lpcbData);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegDeleteValueW(IntPtr hKey, string valueName);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);

    private static string ExePath => Environment.ProcessPath ?? "";

    private static bool IsEnabledWindows()
    {
        if (RegOpenKeyExW(HKEY_CURRENT_USER, RunKey, 0, KEY_READ, out var hKey) != ERROR_SUCCESS)
            return false;
        try
        {
            int size = 0;
            return RegQueryValueExW(hKey, AppName, IntPtr.Zero, out _, IntPtr.Zero, ref size)
                   == ERROR_SUCCESS;
        }
        finally { RegCloseKey(hKey); }
    }

    private static void EnableWindows()
    {
        if (RegOpenKeyExW(HKEY_CURRENT_USER, RunKey, 0, KEY_WRITE, out var hKey) != ERROR_SUCCESS)
            return;
        try
        {
            string value = $"\"{ExePath}\"";
            int cbData = (value.Length + 1) * 2; // UTF-16 + null
            RegSetValueExW(hKey, AppName, 0, REG_SZ, value, cbData);
        }
        finally { RegCloseKey(hKey); }
    }

    private static void DisableWindows()
    {
        if (RegOpenKeyExW(HKEY_CURRENT_USER, RunKey, 0, KEY_WRITE, out var hKey) != ERROR_SUCCESS)
            return;
        try { RegDeleteValueW(hKey, AppName); }
        finally { RegCloseKey(hKey); }
    }

    // ════════════════════════════════════════════════════════════════
    //  Linux — ~/.config/autostart/dblt.desktop
    // ════════════════════════════════════════════════════════════════

    private static string DesktopFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "autostart", "dblt.desktop");

    private static bool IsEnabledLinux() => File.Exists(DesktopFilePath);

    private static void EnableLinux()
    {
        var dir = Path.GetDirectoryName(DesktopFilePath)!;
        Directory.CreateDirectory(dir);

        var exePath = ExePath;
        var content =
            "[Desktop Entry]\n" +
            "Type=Application\n" +
            "Name=DBLT\n" +
            "Comment=Clipboard text normalizer\n" +
            $"Exec={exePath}\n" +
            "Terminal=false\n" +
            "Categories=Utility;\n" +
            "X-GNOME-Autostart-enabled=true\n" +
            "StartupNotify=false\n";

        File.WriteAllText(DesktopFilePath, content);
    }

    private static void DisableLinux()
    {
        try { File.Delete(DesktopFilePath); }
        catch { /* ignore if already gone */ }
    }
}
