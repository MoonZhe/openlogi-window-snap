// snap-third.cs — snap the foreground window to the left / middle / right third
// of the work area of the monitor it is currently on.
//
// Build (no third-party tools; csc.exe ships with Windows .NET Framework):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:snap-third.exe snap-third.cs
//
// Usage: snap-third.exe left | middle | right
// /target:winexe => GUI subsystem, so no console window ever appears.

using System;
using System.Runtime.InteropServices;

static class SnapThird
{
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT value, int size);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    const uint GW_HWNDNEXT      = 2;
    const int  GWL_EXSTYLE      = -20;
    const int  WS_EX_TOOLWINDOW = 0x00000080;
    const int  DWMWA_CLOAKED    = 14;
    const int  DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    // OpenLogi launches us through `cmd /c`, so the foreground window at start is
    // that console (or the action-ring overlay). Walk down the Z-order to the first
    // real, visible, uncloaked app window instead.

    static IntPtr FindTargetWindow()
    {
        var sb = new System.Text.StringBuilder(256);
        for (IntPtr h = GetForegroundWindow(); h != IntPtr.Zero; h = GetWindow(h, GW_HWNDNEXT))
        {
            if (!IsWindowVisible(h) || IsIconic(h)) continue;
            if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) continue;
            int cloaked;
            if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) continue;

            sb.Length = 0; GetClassName(h, sb, sb.Capacity);
            string cls = sb.ToString();
            if (cls == "ConsoleWindowClass" || cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") continue;

            uint pid; GetWindowThreadProcessId(h, out pid);
            string proc = "";
            try { proc = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); } catch { }
            if (proc == "cmd" || proc == "conhost" || proc == "windowsterminal" || proc.StartsWith("openlogi")) continue;

            return h;
        }
        return IntPtr.Zero;
    }
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll", SetLastError = true)] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    const int  SW_RESTORE              = 9;
    const uint MONITOR_DEFAULTTONEAREST = 2;
    const uint SWP_NOZORDER            = 0x0004;
    const uint SWP_NOACTIVATE          = 0x0010;
    const uint SWP_SHOWWINDOW          = 0x0040;

    static int Main(string[] args)
    {
        // Without this, Windows virtualizes coordinates to the primary monitor's DPI and
        // positions/sizes come out wrong on monitors with a different scaling factor.
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        // Zone comes from the first arg, or (when launched with no args, e.g. via
        // OpenLogi's OpenApplication action) from the exe name: snap-left.exe etc.
        string zone = args.Length > 0
            ? args[0].ToLowerInvariant()
            : System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location)
                  .ToLowerInvariant().Replace("snap-", "");
        if (zone != "left" && zone != "middle" && zone != "right") return 2;

        IntPtr hwnd = FindTargetWindow();
        if (hwnd == IntPtr.Zero) return 1;

        // Work area of the monitor the window is actually on (multi-monitor safe).
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi)) return 1;
        RECT wa = mi.rcWork;

        int width  = wa.Right - wa.Left;
        int height = wa.Bottom - wa.Top;
        int half    = width / 2;
        int quarter = width / 4;

        // left/right: halves of the screen; middle: centered 50% (Windows 1/4 | 1/2 | 1/4 layout)
        int x, w;
        switch (zone)
        {
            case "left":   x = wa.Left;           w = half;         break;
            case "middle": x = wa.Left + quarter; w = half;         break;
            default:       x = wa.Left + half;    w = width - half; break;
        }

        ShowWindow(hwnd, SW_RESTORE); // un-maximize first, otherwise SetWindowPos is ignored

        // Win10/11 windows have invisible resize borders: the window rect is larger than the
        // visible frame (DWMWA_EXTENDED_FRAME_BOUNDS). Expand the target rect by that
        // difference so the *visible* edges land exactly on the zone boundaries (no gaps).
        RECT wr, fr;
        int y = wa.Top, h = height;
        if (GetWindowRect(hwnd, out wr) && DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out fr, Marshal.SizeOf(typeof(RECT))) == 0)
        {
            int dl = fr.Left - wr.Left, dt = fr.Top - wr.Top, dr = wr.Right - fr.Right, db = wr.Bottom - fr.Bottom;
            x -= dl; y -= dt; w += dl + dr; h += dt + db;
        }
        return SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h,
                            SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW) ? 0 : 1;
    }
}
