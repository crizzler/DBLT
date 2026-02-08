using System;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// System-tray icon for Windows using Win32 Shell_NotifyIcon + a hidden message window.
/// No WinForms / WPF dependency — pure P/Invoke.
/// </summary>
internal sealed class WindowsTrayIcon : ITrayIcon
{
    // ── Events ─────────────────────────────────────────────────────
    public event Action? ExitRequested;
    public event Action<bool>? AutoStartToggled;

    // ── Win32 constants ────────────────────────────────────────────
    private const uint WM_CLOSE          = 0x0010;
    private const uint WM_DESTROY        = 0x0002;
    private const uint WM_COMMAND        = 0x0111;
    private const uint WM_USER           = 0x0400;
    private const uint WM_TRAYICON       = WM_USER + 1;
    private const uint WM_LBUTTONUP      = 0x0202;
    private const uint WM_RBUTTONUP      = 0x0205;

    private const uint NIM_ADD           = 0x00;
    private const uint NIM_DELETE        = 0x02;
    private const uint NIF_MESSAGE       = 0x01;
    private const uint NIF_ICON          = 0x02;
    private const uint NIF_TIP           = 0x04;

    private const int  IDI_APPLICATION   = 32512;

    private const uint MF_STRING         = 0x0000;
    private const uint MF_SEPARATOR      = 0x0800;
    private const uint MF_CHECKED        = 0x0008;
    private const uint MF_GRAYED         = 0x0001;

    private const uint TPM_BOTTOMALIGN   = 0x0020;
    private const uint TPM_LEFTALIGN     = 0x0000;

    private const int  MENU_AUTOSTART    = 1;
    private const int  MENU_EXIT         = 2;

    // ── Win32 structs ──────────────────────────────────────────────
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    // Minimal NOTIFYICONDATA — only the fields we actually use (V1 compatible).
    // Using a flat byte buffer avoids struct-packing issues across x86/x64.
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    // ── Win32 imports ──────────────────────────────────────────────
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(
        IntPtr hMenu, uint uFlags, int x, int y,
        int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_HIDE = 0;

    // ── State ──────────────────────────────────────────────────────
    private IntPtr _hWnd;
    private NOTIFYICONDATA _nid;
    private bool _autoStartChecked;
    private WndProcDelegate? _wndProc;      // prevent GC
    private uint _wmTaskbarCreated;          // explorer-restart detection
    private bool _disposed;

    public void SetAutoStartChecked(bool isChecked) => _autoStartChecked = isChecked;

    /// <summary>Hides the console window so the app is tray-only.</summary>
    public static void HideConsoleWindow()
    {
        var hConsole = GetConsoleWindow();
        if (hConsole != IntPtr.Zero)
            ShowWindow(hConsole, SW_HIDE);
    }

    // ── Run (blocks on message loop) ───────────────────────────────
    public void Run(CancellationToken ct)
    {
        var hInstance = GetModuleHandle(null);

        _wndProc = WndProc;

        var wcex = new WNDCLASSEX
        {
            cbSize       = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc  = _wndProc,
            hInstance     = hInstance,
            lpszClassName = "DBLTTrayClass",
        };

        if (RegisterClassEx(ref wcex) == 0)
            return; // registration failed — fall through silently

        _hWnd = CreateWindowEx(
            0, "DBLTTrayClass", "DBLT", 0,
            0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hWnd == IntPtr.Zero)
            return; // window creation failed

        var hIcon = LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);

        _nid = new NOTIFYICONDATA
        {
            cbSize           = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd             = _hWnd,
            uID              = 1,
            uFlags           = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon            = hIcon,
            szTip            = "DBLT",
        };

        if (!Shell_NotifyIcon(NIM_ADD, ref _nid))
        {
            // Tray icon failed to add. Clean up and return.
            DestroyWindow(_hWnd);
            return;
        }

        // Re-add the icon if Explorer restarts.
        _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

        // When the CancellationToken fires, post WM_CLOSE from any thread.
        ct.Register(() =>
        {
            if (_hWnd != IntPtr.Zero)
                PostMessage(_hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        });

        // ── Message pump ───────────────────────────────────────────
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    // ── Window procedure ───────────────────────────────────────────
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Tray icon interaction
        if (msg == WM_TRAYICON)
        {
            uint evt = (uint)(lParam.ToInt64() & 0xFFFF);
            if (evt == WM_RBUTTONUP || evt == WM_LBUTTONUP)
                ShowContextMenu();
            return IntPtr.Zero;
        }

        // Menu command
        if (msg == WM_COMMAND)
        {
            int id = (int)(wParam.ToInt64() & 0xFFFF);
            if (id == MENU_EXIT)
            {
                ExitRequested?.Invoke();
                return IntPtr.Zero;
            }
            if (id == MENU_AUTOSTART)
            {
                _autoStartChecked = !_autoStartChecked;
                AutoStartToggled?.Invoke(_autoStartChecked);
                return IntPtr.Zero;
            }
        }

        // Clean shutdown
        if (msg == WM_CLOSE)
        {
            Shell_NotifyIcon(NIM_DELETE, ref _nid);
            DestroyWindow(hWnd);
            return IntPtr.Zero;
        }
        if (msg == WM_DESTROY)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        // Explorer restarted — re-add tray icon
        if (_wmTaskbarCreated != 0 && msg == _wmTaskbarCreated)
        {
            Shell_NotifyIcon(NIM_ADD, ref _nid);
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // ── Context menu ───────────────────────────────────────────────
    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();

        AppendMenu(hMenu, MF_STRING | MF_GRAYED, UIntPtr.Zero, "DBLT is running");
        AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, null);

        uint autoFlags = MF_STRING | (_autoStartChecked ? MF_CHECKED : 0);
        AppendMenu(hMenu, autoFlags, (UIntPtr)MENU_AUTOSTART, "Start with Windows");

        AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, null);
        AppendMenu(hMenu, MF_STRING, (UIntPtr)MENU_EXIT, "Exit");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hWnd);
        TrackPopupMenu(hMenu, TPM_BOTTOMALIGN | TPM_LEFTALIGN, pt.X, pt.Y, 0, _hWnd, IntPtr.Zero);
        DestroyMenu(hMenu);
    }

    // ── Dispose ────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shell_NotifyIcon(NIM_DELETE, ref _nid);
        if (_hWnd != IntPtr.Zero)
            DestroyWindow(_hWnd);
    }
}
