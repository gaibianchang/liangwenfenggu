using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiangWenFengGu
{
    internal static class Program
    {
        private static string ShowEventName
        {
            get
            {
                string desktop = DesktopInfo.DesktopName();
                StringBuilder sb = new StringBuilder("Local\\LiangWenFengGu_Show");
                for (int i = 0; i < desktop.Length; i++)
                {
                    char c = desktop[i];
                    if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
                    else sb.Append('_');
                }
                return sb.ToString();
            }
        }
        private const string ProcessName = "LiangWenFengGu";

        [STAThread]
        private static void Main(string[] args)
        {
            string command = args.Length > 0 ? args[0].ToLowerInvariant() : "";

            if (command == "--render" || command == "--render-settings" || command == "--render-live")
            {
                try
                {
                    string outPath = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "preview.png");
                    string bg = args.Length > 2 ? args[2] : null;
                    CliRenderer.Render(command == "--render-settings", command == "--render-live", outPath, bg);
                }
                catch (Exception ex)
                {
                    WriteError(ex);
                }
                return;
            }

            if (command == "--check")
            {
                AttachConsole(-1);
                AppConfig cfg = AppConfig.Load();
                BalanceResult r = BalanceClient.Query(cfg);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("endpoint: " + r.RawEndpoint);
                sb.AppendLine("config:   " + AppConfig.ConfigPath);
                sb.AppendLine("mode:     " + (BalanceClient.IsOfficial(cfg.BalanceMode) ? "official" : "newapi"));
                sb.AppendLine("ok:       " + (r.Ok ? "true" : "false"));
                if (r.Ok)
                {
                    string symbol = (r.Symbol != null && r.Symbol.Length > 0) ? r.Symbol : cfg.CurrencySymbol;
                    sb.AppendLine("balance:  " + symbol + r.Remaining.ToString("N2", CultureInfo.InvariantCulture));
                    if (r.Official)
                    {
                        if (r.Extra.Length > 0) sb.AppendLine("detail:   " + r.Extra.Replace("\n", " | "));
                    }
                    else
                    {
                        sb.AppendLine("used:     " + symbol + r.Used.ToString("N2", CultureInfo.InvariantCulture));
                        sb.AppendLine("total:    " + symbol + r.Total.ToString("N2", CultureInfo.InvariantCulture));
                    }
                    sb.AppendLine("plan:     " + r.PlanName);
                }
                else
                {
                    sb.AppendLine("error:    " + r.Error);
                }
                string text = sb.ToString();
                Console.Write(text);
                if (args.Length > 1)
                {
                    try { File.WriteAllText(args[1], text, new UTF8Encoding(false)); }
                    catch { }
                }
                return;
            }

            if (command == "--selftest")
            {
                AttachConsole(-1);
                int failed = RunSelfTest();
                Console.Write(failed == 0 ? "\u81EA\u68C0\u901A\u8FC7\uFF08\u5168\u90E8\u7528\u4F8B\u6B63\u786E\uFF09\n" : "\u81EA\u68C0\u5931\u8D25\uFF1A" + failed + " \u4E2A\u7528\u4F8B\u4E0D\u7B26\u5408\u9884\u671F\n");
                Console.Out.Flush();
                Environment.Exit(failed);
                return;
            }
            if (command == "--diag")
            {
                AttachConsole(-1);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("LiangWenFengGu \u8FD0\u884C\u73AF\u5883\u8BCA\u65AD");
                sb.AppendLine("\u65F6\u95F4\uFF1A      " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("\u8FDB\u7A0B\uFF1A      " + AppDomain.CurrentDomain.BaseDirectory);
                sb.AppendLine("\u8EAB\u4EFD\uFF1A      " + DesktopInfo.Describe());
                sb.AppendLine("\u663E\u793A\u5668\uFF1A    " + DesktopInfo.DescribeScreens());
                sb.AppendLine("\u914D\u7F6E\uFF1A      " + AppConfig.ConfigPath);
                sb.AppendLine("\u65E5\u5FD7\uFF1A      " + Log.Path);
                sb.AppendLine("\u5DF2\u5B58\u4F4D\u7F6E\uFF1A  " + AppConfig.Load().HasPosition.ToString() + " (" +
                    AppConfig.Load().Left.ToString("0.#", CultureInfo.InvariantCulture) + ", " +
                    AppConfig.Load().Top.ToString("0.#", CultureInfo.InvariantCulture) + ")");
                sb.AppendLine("\u540C\u540D\u8FDB\u7A0B\uFF1A  " + ExistingInstance.DescribeAll());
                sb.AppendLine("\u7A97\u53E3\u7F6E\u9876\uFF1A  " + (AppConfig.Load().Topmost ? "\u5F00" : "\u5173"));
                string text = sb.ToString();
                Console.Write(text);
                Log.Write("--diag--\r\n" + text);
                if (args.Length > 1)
                {
                    try { File.WriteAllText(args[1], text, new UTF8Encoding(false)); }
                    catch { }
                }
                return;
            }

            if (command == "--autostart")
            {
                AttachConsole(-1);
                string action = args.Length > 1 ? args[1].ToLowerInvariant() : "status";
                if (action == "on" || action == "off" || action == "enable" || action == "disable")
                {
                    bool want = action == "on" || action == "enable";
                    bool applied = AutoStart.Apply(want);
                    bool actual = AutoStart.IsEnabled();
                    Console.Write("apply " + action + ": " + (applied && actual == want ? "ok" : "failed") + "\n");
                }
                Console.Write("autostart enabled: " + (AutoStart.IsEnabled() ? "yes" : "no") + "\n");
                Console.Write("registry value:    " + AutoStart.CurrentValue() + "\n");
                Console.Write("executable:        " + AutoStart.ExecutablePath + "\n");
                return;
            }

            if (command == "--help" || command == "-h" || command == "/?")
            {
                AttachConsole(-1);
                Console.Write("LiangWenFengGu  \u6881\u6587\u5CF0\u8C37 \u684C\u9762\u6446\u4EF6\n");
                Console.Write("  (\u65E0\u53C2\u6570)           \u542F\u52A8\u6446\u4EF6\uFF08\u5DF2\u6709\u53EF\u89C1\u5B9E\u4F8B\u65F6\u5524\u8D77\u5B83\uFF09\n");
                Console.Write("  --show                \u5524\u8D77\u6B63\u5728\u8FD0\u884C\u7684\u6446\u4EF6\n");
                Console.Write("  --check [file]        \u67E5\u8BE2\u4F59\u989D\u5E76\u8F93\u51FA\n");
                Console.Write("  --diag [file]         \u8F93\u51FA\u8FD0\u884C\u73AF\u5883\u8BCA\u65AD\u4FE1\u606F\n");
                Console.Write("  --selftest            \u81EA\u68C0\uFF08\u540D\u5B57\u989C\u8272\u4E0E\u5012\u8BA1\u65F6\u7528\u4F8B\uFF09\n");
                Console.Write("  --autostart on|off|status   \u8BBE\u7F6E/\u67E5\u770B\u5F00\u673A\u81EA\u542F\u52A8\n");
                Console.Write("  --render file [bg]           \u6E32\u67D3\u6446\u4EF6\u9884\u89C8\u56FE\uFF08\u6837\u4F8B\u6570\u636E\uFF09\n");
                Console.Write("  --render-live file [bg]      \u6E32\u67D3\u6446\u4EF6\u5F53\u524D\u5B9E\u9645\u72B6\u6001\uFF08\u771F\u5B9E\u65F6\u95F4\uFF09\n");
                Console.Write("  --render-settings file [bg]  \u6E32\u67D3\u8BBE\u7F6E\u754C\u9762\u9884\u89C8\u56FE\n");
                return;
            }

            bool wantShow = command == "--show";

            EventWaitHandle showEvent = null;
            bool createdNew = true;
            try
            {
                showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName, out createdNew);
            }
            catch
            {
                createdNew = true;
                showEvent = null;
            }

            IntPtr peer = ExistingInstance.FindVisibleWindow();

            if (peer != IntPtr.Zero && (!createdNew || wantShow))
            {
                try { if (showEvent != null) showEvent.Set(); }
                catch { }
                if (ExistingInstance.PeerVisible) ExistingInstance.RaiseWindow(peer);
                Log.Write("\u5DF2\u6709\u5B9E\u4F8B\uFF0C\u672C\u6B21\u542F\u52A8\u8F6C\u4E3A\u5524\u9192\u3002 " + DesktopInfo.Describe());
                return;
            }

            if (!createdNew)
            {
                Log.Write("\u540C\u540D\u5BF9\u8C61\u5DF2\u5B58\u5728\u4F46\u672C\u684C\u9762\u627E\u4E0D\u5230\u53EF\u89C1\u7A97\u53E3\uFF0C\u7EE7\u7EED\u542F\u52A8\u3002 " + DesktopInfo.Describe());
            }

            try
            {
                RunWidget(showEvent, args);
            }
            catch (Exception ex)
            {
                WriteError(ex);
                try
                {
                    MessageBox.Show("\u6881\u6587\u5CF0\u8C37\u542F\u52A8\u5931\u8D25\uFF1A\n" + ex.Message +
                        "\n\n\u8BE6\u60C5\u5DF2\u5199\u5165 " + Log.Path,
                        "\u6881\u6587\u5CF0\u8C37", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            }
        }

        private static void RunWidget(EventWaitHandle showEvent, string[] args)
        {
            Application application = new Application();
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            application.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Write("\u672A\u5904\u7406\u5F02\u5E38\uFF1A" + e.Exception);
                e.Handled = true;
            };

            Log.Write("=== \u542F\u52A8 " + typeof(Program).Assembly.GetName().Version + " === " + DesktopInfo.Describe());
            Log.Write("\u663E\u793A\u5668\uFF1A" + DesktopInfo.DescribeScreens());

            AppConfig config = AppConfig.Load();
            Log.Write("\u914D\u7F6E\uFF1A" + AppConfig.ConfigPath + " | HasPosition=" + config.HasPosition +
                " Left=" + config.Left.ToString("0.#", CultureInfo.InvariantCulture) +
                " Top=" + config.Top.ToString("0.#", CultureInfo.InvariantCulture) +
                " Scale=" + config.Scale.ToString("0.##", CultureInfo.InvariantCulture));

            if (config.AutoStart) AutoStart.Apply(true);
            else AutoStart.Repair();

            WidgetWindow widget = new WidgetWindow(config);
            widget.Show();
            Log.Write("\u7A97\u53E3\u5DF2\u663E\u793A\uFF1ALeft=" + widget.Left.ToString("0.#", CultureInfo.InvariantCulture) +
                " Top=" + widget.Top.ToString("0.#", CultureInfo.InvariantCulture) +
                " W=" + widget.Width.ToString("0.#", CultureInfo.InvariantCulture) +
                " H=" + widget.Height.ToString("0.#", CultureInfo.InvariantCulture) +
                " Visible=" + widget.IsVisible);

            string wakeLog = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "--wake-log", StringComparison.OrdinalIgnoreCase)) wakeLog = args[i + 1];
            }

            if (showEvent != null)
            {
                Thread listener = new Thread(delegate()
                {
                    while (true)
                    {
                        try { showEvent.WaitOne(); }
                        catch { return; }
                        try
                        {
                            application.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                Log.Write("\u6536\u5230\u5524\u9192\u4FE1\u53F7");
                                widget.BringToFront();
                                if (wakeLog != null) AppendLog(wakeLog, "wake " + DateTime.Now.ToString("s"));
                            }));
                        }
                        catch { return; }
                    }
                });
                listener.IsBackground = true;
                listener.Start();
            }

            application.Run();
        }

        private static int RunSelfTest()
        {
            return SelfTest.Run();
        }

        private static void AppendLog(string path, string line)
        {
            try { File.AppendAllText(path, line + "\r\n", new UTF8Encoding(false)); }
            catch { }
        }

        private static void WriteError(Exception ex)
        {
            Log.Write("\u4E25\u91CD\u9519\u8BEF\uFF1A" + ex);
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.AppendAllText(path, DateTime.Now.ToString("s") + "  " + ex.ToString() + "\r\n\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
    }

    internal static class SelfTest
    {
        private class Case
        {
            public DateTime At;
            public string Name;
            public string Color;
            public string Target;
            public Case(DateTime at, string name, string color, string target)
            {
                At = at;
                Name = name;
                Color = color;
                Target = target;
            }
        }

        public static int Run()
        {
            Case[] cases = new Case[]
            {
                new Case(new DateTime(2026, 9, 17, 8, 30, 0),  "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 17, 9, 0, 0),   "\u6881\u6587\u5CF0", "#FFFF453A", "\u8DDD\u6881\u6587\u8C37"),
                new Case(new DateTime(2026, 9, 17, 10, 15, 30),"\u6881\u6587\u5CF0", "#FFFF453A", "\u8DDD\u6881\u6587\u8C37"),
                new Case(new DateTime(2026, 9, 17, 12, 0, 0),  "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 17, 12, 30, 0), "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 17, 14, 0, 0),  "\u6881\u6587\u5CF0", "#FFFF453A", "\u8DDD\u6881\u6587\u8C37"),
                new Case(new DateTime(2026, 9, 17, 18, 0, 0),  "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 18, 18, 30, 0), "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 19, 11, 0, 0),  "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 20, 12, 0, 0),  "\u6881\u6587\u8C37", "#FF32D74B", "\u8DDD\u6881\u6587\u5CF0"),
                new Case(new DateTime(2026, 9, 21, 9, 0, 0),   "\u6881\u6587\u5CF0", "#FFFF453A", "\u8DDD\u6881\u6587\u8C37")
            };

            int failed = 0;
            WidgetWindow widget = new WidgetWindow(AppConfig.Load(), true);
            foreach (Case c in cases)
            {
                widget.UpdateClockAt(c.At);
                string state = widget.DebugState();
                string[] parts = state.Split('|');
                bool ok = parts.Length == 3 && parts[0] == c.Name && parts[1] == c.Color && parts[2].StartsWith(c.Target);
                if (!ok) failed++;
                Console.Write((ok ? "PASS  " : "FAIL  ") + c.At.ToString("yyyy-MM-dd HH:mm:ss") +
                    "  \u671F\u671B " + c.Name + " " + c.Color + " " + c.Target +
                    "  \u5B9E\u9645 " + state + "\n");
            }
            widget.Close();
            return failed;
        }
    }
    internal static class ExistingInstance
    {
        public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static readonly System.Collections.Generic.List<string> Found = new System.Collections.Generic.List<string>();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private const string WidgetTitle = "\u6881\u6587\u5CF0\u8C37";

        // 桌面组件模式下摆件可能处于隐藏状态，这里把隐藏窗口也算作已有实例，
        // 避免再次双击 exe 时又启动一个重复的摆件。
        public static bool PeerVisible { get; private set; }

        private static string TitleOf(IntPtr hWnd)
        {
            try
            {
                StringBuilder sb = new StringBuilder(256);
                int len = GetWindowTextW(hWnd, sb, sb.Capacity);
                return len > 0 ? sb.ToString(0, len) : "";
            }
            catch { return ""; }
        }

        public static IntPtr FindVisibleWindow()
        {
            Found.Clear();
            IntPtr result = IntPtr.Zero;
            IntPtr fallback = IntPtr.Zero;
            IntPtr hidden = IntPtr.Zero;
            PeerVisible = false;
            try
            {
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    uint pid;
                    GetWindowThreadProcessId(hWnd, out pid);
                    if (pid == 0) return true;
                    string name = ProcessNameOf(pid);
                    if (name == null) return true;
                    Rect rect;
                    if (!GetWindowRect(hWnd, out rect)) return true;
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    bool visible = IsWindowVisible(hWnd);
                    Found.Add("pid=" + pid + " visible=" + visible + " size=" + w + "x" + h);
                    if (w <= 150 || h <= 80) return true;
                    if (TitleOf(hWnd) == WidgetTitle)
                    {
                        if (result == IntPtr.Zero)
                        {
                            result = hWnd;
                            PeerVisible = visible;
                        }
                    }
                    else if (visible)
                    {
                        if (fallback == IntPtr.Zero) fallback = hWnd;
                    }
                    else if (hidden == IntPtr.Zero)
                    {
                        hidden = hWnd;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            if (result != IntPtr.Zero) return result;
            if (fallback != IntPtr.Zero)
            {
                PeerVisible = true;
                return fallback;
            }
            PeerVisible = false;
            return hidden;
        }

        private static string ProcessNameOf(uint pid)
        {
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    if (p.ProcessName == "LiangWenFengGu") return p.ProcessName;
                }
            }
            catch { }
            return null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmdShow);

        private const int SwRestore = 9;

        public static void RaiseWindow(IntPtr hWnd)
        {
            try
            {
                ShowWindow(hWnd, SwRestore);
                SetForegroundWindow(hWnd);
            }
            catch { }
        }

        public static string DescribeAll()
        {
            FindVisibleWindow();
            if (Found.Count == 0) return "\u65E0\uFF08\u5F53\u524D\u684C\u9762\u4E0A\u6CA1\u6709\u5176\u4ED6\u6446\u4EF6\u7A97\u53E3\uFF09";
            return string.Join("; ", Found.ToArray());
        }
    }

    internal static class CliRenderer
    {
        public static void Render(bool settings, bool live, string path, string bgHex)
        {
            double height;

            if (settings)
            {
                AppConfig cfg = AppConfig.Load();
                WidgetWindow host = new WidgetWindow(cfg, true);
                SettingsWindow win = new SettingsWindow(host);
                Grid root = (Grid)win.Content;
                win.Content = null;
                ((TextBox)root.FindName("BaseUrlBox")).Text = "https://relay.example.com";
                ((TextBox)root.FindName("TokenBox")).Text = "sk-DEMO-TOKEN-FOR-SCREENSHOT";
                ((TextBox)root.FindName("UserIdBox")).Text = "10001";
                Border panel = (Border)root.FindName("StatusPanel");
                panel.Visibility = Visibility.Visible;
                TextBlock status = (TextBlock)root.FindName("StatusText");
                status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x6E, 0xD9, 0x8A));
                status.Text = "\u8FDE\u63A5\u6210\u529F \u00B7 \u5F53\u524D\u4F59\u989D \u00A512.34\uFF08\u9ED8\u8BA4\u5957\u9910\uFF09\n\u5DF2\u7528 \u00A51.06 \u00B7 \u603B\u989D \u00A513.40\nhttps://relay.example.com/api/user/self";
                Grid wrap = new Grid();
                wrap.Width = 470;
                wrap.Children.Add(root);
                wrap.Measure(new Size(470, double.PositiveInfinity));
                height = Math.Ceiling(wrap.DesiredSize.Height);
                wrap.Arrange(new Rect(0, 0, 470, height));
                wrap.UpdateLayout();
                Save(wrap, 470, height, path, bgHex);
                win.ForceClose();
                host.Close();
                return;
            }

            AppConfig config = AppConfig.Load();
            WidgetWindow widget = new WidgetWindow(config, true);
            if (!live) widget.SetPreviewSample();
            Grid widgetRoot = widget.TakeRoot();
            Grid container = new Grid();
            container.Width = WidgetWindow.DesignWidth;
            container.Height = WidgetWindow.DesignHeight;
            if (!string.IsNullOrEmpty(bgHex))
            {
                try { container.Background = (Brush)new BrushConverter().ConvertFromString(bgHex); }
                catch { }
            }
            container.Children.Add(widgetRoot);
            container.Measure(new Size(WidgetWindow.DesignWidth, WidgetWindow.DesignHeight));
            container.Arrange(new Rect(0, 0, WidgetWindow.DesignWidth, WidgetWindow.DesignHeight));
            container.UpdateLayout();
            Save(container, WidgetWindow.DesignWidth, WidgetWindow.DesignHeight, path, null);
        }

        private static void Save(FrameworkElement visual, double width, double height, string path, string bgHex)
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap(
                (int)Math.Round(width * 2), (int)Math.Round(height * 2), 192, 192, PixelFormats.Pbgra32);
            bmp.Render(visual);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using (FileStream fs = File.Create(path))
            {
                enc.Save(fs);
            }
        }
    }
}
