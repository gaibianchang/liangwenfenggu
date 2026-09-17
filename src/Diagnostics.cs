using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LiangWenFengGu
{
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;
        private const long MaxBytes = 256 * 1024;
        private const long KeepBytes = 64 * 1024;

        public static string Path
        {
            get
            {
                if (_path != null) return _path;
                try
                {
                    _path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "\u8FD0\u884C\u65E5\u5FD7.txt");
                }
                catch { }
                if (_path == null)
                {
                    string dir = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LiangWenFengGu");
                    try { Directory.CreateDirectory(dir); }
                    catch { }
                    _path = System.IO.Path.Combine(dir, "\u8FD0\u884C\u65E5\u5FD7.txt");
                }
                return _path;
            }
        }

        public static void Write(string message)
        {
            lock (Gate)
            {
                try
                {
                    string path = Path;
                    Trim(path);
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n",
                        new UTF8Encoding(false));
                }
                catch { }
            }
        }

        private static void Trim(string path)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length <= MaxBytes) return;
                byte[] tail;
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long start = fs.Length - KeepBytes;
                    fs.Seek(start, SeekOrigin.Begin);
                    tail = new byte[KeepBytes];
                    int read = fs.Read(tail, 0, tail.Length);
                    if (read < tail.Length)
                    {
                        byte[] shrink = new byte[read];
                        Array.Copy(tail, shrink, read);
                        tail = shrink;
                    }
                }
                File.WriteAllBytes(path, tail);
            }
            catch { }
        }
    }

    public static class DesktopInfo
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDesktop(int threadId);

        [DllImport("kernel32.dll")]
        private static extern int GetCurrentThreadId();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserObjectInformationW(IntPtr hObj, int index, StringBuilder info, int length, out int lengthNeeded);

        [StructLayout(LayoutKind.Sequential)]
        private struct USEROBJECTFLAGS
        {
            public int fInherit;
            public int fReserved;
            public int dwFlags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetUserObjectInformationW(IntPtr hObj, int index, ref USEROBJECTFLAGS info, int length, out int lengthNeeded);

        private const int UoiFlags = 1;
        private const int UoiName = 2;
        private const int WsfVisible = 0x0001;

        public static string WindowStationName()
        {
            return Name(GetProcessWindowStation());
        }

        public static string DesktopName()
        {
            return Name(GetThreadDesktop(GetCurrentThreadId()));
        }

        private static string Name(IntPtr handle)
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                int needed;
                if (GetUserObjectInformationW(handle, UoiName, sb, sb.Capacity * 2, out needed)) return sb.ToString();
            }
            catch { }
            return "?";
        }

        public static bool WindowStationVisible()
        {
            try
            {
                USEROBJECTFLAGS flags = new USEROBJECTFLAGS();
                int needed;
                if (GetUserObjectInformationW(GetProcessWindowStation(), UoiFlags, ref flags, Marshal.SizeOf(typeof(USEROBJECTFLAGS)), out needed))
                {
                    return (flags.dwFlags & WsfVisible) == WsfVisible;
                }
            }
            catch { }
            return false;
        }

        public static string Describe()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                Process p = Process.GetCurrentProcess();
                sb.Append("user=").Append(Environment.UserDomainName).Append("\\").Append(Environment.UserName);
                sb.Append(" session=").Append(p.SessionId);
                sb.Append(" winsta=").Append(WindowStationName());
                sb.Append(" desktop=").Append(DesktopName());
                sb.Append(" winsta_visible=").Append(WindowStationVisible() ? "yes" : "no");
            }
            catch (Exception ex)
            {
                sb.Append("describe failed: ").Append(ex.Message);
            }
            return sb.ToString();
        }

        public static string DescribeScreens()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (sb.Length > 0) sb.Append(" | ");
                    sb.Append(screen.DeviceName).Append(" bounds=").Append(screen.Bounds)
                      .Append(" work=").Append(screen.WorkingArea)
                      .Append(screen.Primary ? " primary" : "");
                }
            }
            catch (Exception ex)
            {
                sb.Append("screens failed: ").Append(ex.Message);
            }
            return sb.ToString();
        }
    }
}
