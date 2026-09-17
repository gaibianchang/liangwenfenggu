using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LiangWenFengGu
{
    // 判断「现在是不是在看桌面」，以及在桌面出现时把摆件抬到普通窗口的最上层。
    // 桌面窗口是 Progman / WorkerW；装了桌面美化软件（如小智桌面）时，
    // 桌面图层可能由它自己的进程绘制，所以把这类进程也算作桌面。
    public static class DesktopFocus
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint flags);

        private static readonly IntPtr HwndTop = IntPtr.Zero;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoActivate = 0x0010;

        private static readonly string[] DesktopClasses = new string[] { "Progman", "WorkerW", "SysListView32" };
        private static readonly string[] DesktopProcesses = new string[] { "XZDesktop64", "XZDesktop", "XZWidget" };

        public static IntPtr Foreground
        {
            get
            {
                try { return GetForegroundWindow(); }
                catch { return IntPtr.Zero; }
            }
        }

        public static string ClassName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            try
            {
                StringBuilder sb = new StringBuilder(256);
                int len = GetClassName(hWnd, sb, sb.Capacity);
                return len > 0 ? sb.ToString(0, len) : "";
            }
            catch { return ""; }
        }

        public static string ProcessName(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "";
            try
            {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return "";
                using (Process p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return ""; }
        }

        public static bool DesktopIsForeground()
        {
            IntPtr fg = Foreground;
            if (fg == IntPtr.Zero) return false;
            string cls = ClassName(fg);
            for (int i = 0; i < DesktopClasses.Length; i++)
            {
                if (string.Equals(cls, DesktopClasses[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            string proc = ProcessName(fg);
            for (int i = 0; i < DesktopProcesses.Length; i++)
            {
                if (string.Equals(proc, DesktopProcesses[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // 只抬到「普通窗口的最上层」，不抢焦点、不置顶，
        // 这样其他软件一被点击仍然会盖在摆件上面。
        public static void RaiseAboveNormalWindows(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try { SetWindowPos(hWnd, HwndTop, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate); }
            catch { }
        }

        public static string Describe()
        {
            IntPtr fg = Foreground;
            return "class=" + ClassName(fg) + " proc=" + ProcessName(fg) + " desktop=" + (DesktopIsForeground() ? "yes" : "no");
        }
    }
}