using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

/// <summary>
/// System-tray icon for Linux using libayatana-appindicator3 (or legacy
/// libappindicator3) + GTK3.  Falls back gracefully if libraries are missing.
/// </summary>
internal sealed class LinuxTrayIcon : ITrayIcon
{
    // ── Events ─────────────────────────────────────────────────────
    public event Action? ExitRequested;
    public event Action<bool>? AutoStartToggled;

    // ── Delegates for native callbacks ─────────────────────────────
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GtkCallback(IntPtr widget, IntPtr data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool GSourceFunc(IntPtr data);

    // ── GTK3 P/Invoke ──────────────────────────────────────────────
    [DllImport("libgtk-3.so.0")]
    private static extern bool gtk_init_check(IntPtr argc, IntPtr argv);

    [DllImport("libgtk-3.so.0")]
    private static extern void gtk_main();

    [DllImport("libgtk-3.so.0")]
    private static extern void gtk_main_quit();

    [DllImport("libgtk-3.so.0")]
    private static extern IntPtr gtk_menu_new();

    [DllImport("libgtk-3.so.0", CharSet = CharSet.Ansi)]
    private static extern IntPtr gtk_menu_item_new_with_label(string label);

    [DllImport("libgtk-3.so.0", CharSet = CharSet.Ansi)]
    private static extern IntPtr gtk_check_menu_item_new_with_label(string label);

    [DllImport("libgtk-3.so.0")]
    private static extern void gtk_check_menu_item_set_active(IntPtr item, bool isActive);

    [DllImport("libgtk-3.so.0")]
    private static extern bool gtk_check_menu_item_get_active(IntPtr item);

    [DllImport("libgtk-3.so.0")]
    private static extern void gtk_menu_shell_append(IntPtr shell, IntPtr child);

    [DllImport("libgtk-3.so.0")]
    private static extern IntPtr gtk_separator_menu_item_new();

    [DllImport("libgtk-3.so.0")]
    private static extern void gtk_widget_show_all(IntPtr widget);

    [DllImport("libgtk-3.so.0")]
    private static extern void gtk_widget_set_sensitive(IntPtr widget, bool sensitive);

    // ── GObject signal ─────────────────────────────────────────────
    [DllImport("libgobject-2.0.so.0", CharSet = CharSet.Ansi)]
    private static extern ulong g_signal_connect_data(
        IntPtr instance, string detailedSignal, GtkCallback handler,
        IntPtr data, IntPtr destroyData, int connectFlags);

    // ── GLib idle (thread-safe quit) ───────────────────────────────
    [DllImport("libglib-2.0.so.0")]
    private static extern uint g_idle_add(GSourceFunc function, IntPtr data);

    // ── AppIndicator ───────────────────────────────────────────────
    // The DllImport name "appindicator3" is resolved at runtime by
    // SetDllImportResolver to libayatana-appindicator3.so.1 or
    // libappindicator3.so.1 — whichever is installed.

    private const int APP_INDICATOR_CATEGORY_APPLICATION_STATUS = 0;
    private const int APP_INDICATOR_STATUS_ACTIVE = 1;

    [DllImport("appindicator3", CharSet = CharSet.Ansi)]
    private static extern IntPtr app_indicator_new(string id, string iconName, int category);

    [DllImport("appindicator3")]
    private static extern void app_indicator_set_status(IntPtr indicator, int status);

    [DllImport("appindicator3")]
    private static extern void app_indicator_set_menu(IntPtr indicator, IntPtr menu);

    // ── DLL import resolver (ayatana vs legacy) ────────────────────
    static LinuxTrayIcon()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(LinuxTrayIcon).Assembly, ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(
        string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "appindicator3")
        {
            if (NativeLibrary.TryLoad(
                    "libayatana-appindicator3.so.1", assembly, searchPath, out var h))
                return h;
            if (NativeLibrary.TryLoad(
                    "libappindicator3.so.1", assembly, searchPath, out h))
                return h;
        }
        // Everything else → default resolution.
        return IntPtr.Zero;
    }

    // ── Availability check (call before constructing) ──────────────
    public static bool IsAvailable()
    {
        if (!NativeLibrary.TryLoad("libgtk-3.so.0", out _))
            return false;

        return NativeLibrary.TryLoad("libayatana-appindicator3.so.1", out _)
            || NativeLibrary.TryLoad("libappindicator3.so.1", out _);
    }

    // ── State ──────────────────────────────────────────────────────
    private bool _autoStartChecked;
    private bool _suppressToggle;
    private bool _disposed;

    // prevent GC of native callbacks
    private GtkCallback? _onExitActivate;
    private GtkCallback? _onAutoStartToggled;
    private GSourceFunc? _quitFunc;

    public void SetAutoStartChecked(bool isChecked) => _autoStartChecked = isChecked;

    // ── Run (blocks on gtk_main) ───────────────────────────────────
    public void Run(CancellationToken ct)
    {
        if (!gtk_init_check(IntPtr.Zero, IntPtr.Zero))
            throw new InvalidOperationException("gtk_init_check failed.");

        // ── Build the menu ─────────────────────────────────────────
        var menu = gtk_menu_new();

        // "DBLT is running" (greyed-out label)
        var labelItem = gtk_menu_item_new_with_label("DBLT is running");
        gtk_widget_set_sensitive(labelItem, false);
        gtk_menu_shell_append(menu, labelItem);

        gtk_menu_shell_append(menu, gtk_separator_menu_item_new());

        // "Start with system" (toggle)
        var autoStartItem = gtk_check_menu_item_new_with_label("Start with system");

        _suppressToggle = true;
        gtk_check_menu_item_set_active(autoStartItem, _autoStartChecked);
        _suppressToggle = false;

        _onAutoStartToggled = OnAutoStartToggled;
        g_signal_connect_data(autoStartItem, "toggled", _onAutoStartToggled,
            IntPtr.Zero, IntPtr.Zero, 0);
        gtk_menu_shell_append(menu, autoStartItem);

        gtk_menu_shell_append(menu, gtk_separator_menu_item_new());

        // "Exit"
        var exitItem = gtk_menu_item_new_with_label("Exit");
        _onExitActivate = OnExitActivate;
        g_signal_connect_data(exitItem, "activate", _onExitActivate,
            IntPtr.Zero, IntPtr.Zero, 0);
        gtk_menu_shell_append(menu, exitItem);

        gtk_widget_show_all(menu);

        // ── Create indicator ───────────────────────────────────────
        var indicator = app_indicator_new(
            "dblt-clipboard", "edit-paste",
            APP_INDICATOR_CATEGORY_APPLICATION_STATUS);

        app_indicator_set_status(indicator, APP_INDICATOR_STATUS_ACTIVE);
        app_indicator_set_menu(indicator, menu);

        // ── Quit from another thread via g_idle_add ────────────────
        _quitFunc = _ => { gtk_main_quit(); return false; };
        ct.Register(() => g_idle_add(_quitFunc, IntPtr.Zero));

        // ── Enter GTK main loop (blocks) ───────────────────────────
        gtk_main();
    }

    // ── Callbacks ──────────────────────────────────────────────────
    private void OnAutoStartToggled(IntPtr widget, IntPtr data)
    {
        if (_suppressToggle) return;
        bool active = gtk_check_menu_item_get_active(widget);
        AutoStartToggled?.Invoke(active);
    }

    private void OnExitActivate(IntPtr widget, IntPtr data)
    {
        ExitRequested?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // GTK cleans up when gtk_main returns.
    }
}
