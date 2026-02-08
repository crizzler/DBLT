using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

/// <summary>
/// Clipboard provider for Windows using Win32 P/Invoke.
/// </summary>
internal sealed class WindowsClipboardProvider : IClipboardProvider
{
    // ── Win32 imports ──────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE  = 0x0002;

    // ── Read ───────────────────────────────────────────────────────
    public Task<string?> GetTextAsync()
    {
        string? result = null;

        if (!OpenClipboard(IntPtr.Zero))
            return Task.FromResult(result);

        try
        {
            var hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == IntPtr.Zero)
                return Task.FromResult(result);

            var pData = GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return Task.FromResult(result);

            try
            {
                result = Marshal.PtrToStringUni(pData);
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
        finally
        {
            CloseClipboard();
        }

        return Task.FromResult(result);
    }

    // ── Write ──────────────────────────────────────────────────────
    public Task SetTextAsync(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return Task.CompletedTask;

        try
        {
            EmptyClipboard();

            int byteCount = (text.Length + 1) * sizeof(char); // +1 for null terminator
            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
            if (hGlobal == IntPtr.Zero)
                return Task.CompletedTask;

            var pGlobal = GlobalLock(hGlobal);
            if (pGlobal == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return Task.CompletedTask;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, pGlobal, text.Length);
                Marshal.WriteInt16(pGlobal, text.Length * sizeof(char), 0); // null terminator
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            // After SetClipboardData succeeds the system owns the memory.
            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                GlobalFree(hGlobal);
        }
        finally
        {
            CloseClipboard();
        }

        return Task.CompletedTask;
    }
}
