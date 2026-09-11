// snap.cs — move/resize the foreground window to a named zone of its monitor's work area.
//
// Build (csc.exe ships with Windows .NET Framework 4.x; see build.ps1):
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /optimize /out:snap.exe snap.cs
//
// Usage:  snap.exe <zone>          or, with no args, the zone is taken from the exe's
//         own filename: snap-<zone>.exe  (OpenLogi's OpenApplication passes no arguments)
//
// Zones:  left right top bottom middle            halves (middle = centered half)
//         top-left top-right bottom-left bottom-right   quarters
//         left-third middle-third right-third       thirds
//         left-two-thirds right-two-thirds
//         center                                    floating, 80% x 80%, centered
//         maximize minimize restore
//         next-monitor prev-monitor                 move to another screen, same relative rect
//
// /target:winexe => GUI subsystem, so no console window ever appears.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

static class Snap
{
    // ---- Win32 ---------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT value, int size);

    const uint GW_HWNDNEXT                 = 2;
    const int  GWL_EXSTYLE                 = -20;
    const int  WS_EX_TOOLWINDOW            = 0x00000080;
    const int  DWMWA_CLOAKED               = 14;
    const int  DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    const int  SW_RESTORE                  = 9;
    const int  SW_MAXIMIZE                 = 3;
    const int  SW_MINIMIZE                 = 6;
    const uint MONITOR_DEFAULTTONEAREST    = 2;
    const uint SWP_NOZORDER                = 0x0004;
    const uint SWP_NOACTIVATE              = 0x0010;
    const uint SWP_SHOWWINDOW              = 0x0040;
    static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    // ---- zone table: fractions of the work area (x0, x1, y0, y1) in 12ths -------------------
    // Using shared edge fractions (not widths) guarantees adjacent zones tile with no seam.

    struct Zone { public int X0, X1, Y0, Y1; public Zone(int x0, int x1, int y0, int y1) { X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; } }
    const int DEN = 12;

    static readonly Dictionary<string, Zone> Zones = new Dictionary<string, Zone>
    {
        { "left",            new Zone(0, 6,  0, 12) },
        { "right",           new Zone(6, 12, 0, 12) },
        { "middle",          new Zone(3, 9,  0, 12) },   // centered half  (¼ · ½ · ¼)
        { "top",             new Zone(0, 12, 0, 6)  },
        { "bottom",          new Zone(0, 12, 6, 12) },
        { "top-left",        new Zone(0, 6,  0, 6)  },
        { "top-right",       new Zone(6, 12, 0, 6)  },
        { "bottom-left",     new Zone(0, 6,  6, 12) },
        { "bottom-right",    new Zone(6, 12, 6, 12) },
        { "left-third",      new Zone(0, 4,  0, 12) },
        { "middle-third",    new Zone(4, 8,  0, 12) },
        { "right-third",     new Zone(8, 12, 0, 12) },
        { "left-two-thirds", new Zone(0, 8,  0, 12) },
        { "right-two-thirds",new Zone(4, 12, 0, 12) },
        { "center",          new Zone(1, 11, 1, 11) },   // floating, ~83% each way
    };

    // ---- entry ---------------------------------------------------------------------------

    static int Main(string[] args)
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

        string zone = args.Length > 0
            ? args[0].ToLowerInvariant()
            : System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetExecutingAssembly().Location)
                  .ToLowerInvariant().Replace("snap-", "");

        IntPtr hwnd = FindTargetWindow();
        if (hwnd == IntPtr.Zero) return 1;

        switch (zone)
        {
            case "maximize":     return ShowWindow(hwnd, SW_MAXIMIZE) ? 0 : 1;
            case "minimize":     return ShowWindow(hwnd, SW_MINIMIZE) ? 0 : 1;
            case "restore":      return ShowWindow(hwnd, SW_RESTORE)  ? 0 : 1;
            case "next-monitor": return MoveToMonitor(hwnd, +1) ? 0 : 1;
            case "prev-monitor": return MoveToMonitor(hwnd, -1) ? 0 : 1;
        }

        Zone z;
        if (!Zones.TryGetValue(zone, out z)) return 2;

        RECT wa = WorkAreaOf(hwnd);
        int width = wa.Right - wa.Left, height = wa.Bottom - wa.Top;
        int x0 = wa.Left + width  * z.X0 / DEN, x1 = wa.Left + width  * z.X1 / DEN;
        int y0 = wa.Top  + height * z.Y0 / DEN, y1 = wa.Top  + height * z.Y1 / DEN;
        return Place(hwnd, x0, y0, x1 - x0, y1 - y0) ? 0 : 1;
    }

    // ---- helpers -------------------------------------------------------------------------

    // OpenLogi (and cmd /c) may be in front at start; walk down the Z-order to the first
    // real, visible, uncloaked app window.
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

    static RECT WorkAreaOf(IntPtr hwnd)
    {
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
        GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi);
        return mi.rcWork;
    }

    // Un-maximize, then position so the *visible* frame (not the window rect, which includes
    // Win10/11's invisible resize borders) lands exactly on the requested rect.
    static bool Place(IntPtr hwnd, int x, int y, int w, int h)
    {
        ShowWindow(hwnd, SW_RESTORE);
        RECT wr, fr;
        if (GetWindowRect(hwnd, out wr) &&
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out fr, Marshal.SizeOf(typeof(RECT))) == 0)
        {
            int dl = fr.Left - wr.Left, dt = fr.Top - wr.Top, dr = wr.Right - fr.Right, db = wr.Bottom - fr.Bottom;
            x -= dl; y -= dt; w += dl + dr; h += dt + db;
        }
        return SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    // Move the window to the next/previous monitor (ordered left-to-right), keeping the same
    // position and size *relative to the work area*, so a left-half window stays a left-half.
    static bool MoveToMonitor(IntPtr hwnd, int step)
    {
        var monitors = new List<MONITORINFO>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr hm, IntPtr hdc, ref RECT r, IntPtr d)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(hm, ref mi)) monitors.Add(mi);
            return true;
        }, IntPtr.Zero);
        if (monitors.Count < 2) return false;
        monitors.Sort(delegate (MONITORINFO a, MONITORINFO b) { return a.rcMonitor.Left.CompareTo(b.rcMonitor.Left); });

        RECT cur = WorkAreaOf(hwnd);
        int idx = monitors.FindIndex(delegate (MONITORINFO m) { return m.rcWork.Left == cur.Left && m.rcWork.Top == cur.Top; });
        if (idx < 0) idx = 0;
        RECT dst = monitors[(idx + step + monitors.Count) % monitors.Count].rcWork;

        RECT fr;
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out fr, Marshal.SizeOf(typeof(RECT))) != 0)
            GetWindowRect(hwnd, out fr);

        double cw = cur.Right - cur.Left, ch = cur.Bottom - cur.Top;
        double dw = dst.Right - dst.Left, dh = dst.Bottom - dst.Top;
        int x = dst.Left + (int)Math.Round((fr.Left - cur.Left) / cw * dw);
        int y = dst.Top  + (int)Math.Round((fr.Top  - cur.Top)  / ch * dh);
        int w = (int)Math.Round((fr.Right  - fr.Left) / cw * dw);
        int h = (int)Math.Round((fr.Bottom - fr.Top)  / ch * dh);
        return Place(hwnd, x, y, w, h);
    }
}
