using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LiangWenFengGu
{
    public class WidgetWindow : Window
    {
        public const double DesignWidth = 380;
        public const double DesignHeight = 236;

        private static readonly Color FengColor = Color.FromRgb(0xFF, 0x45, 0x3A);
        private static readonly Color CardTopColor = Color.FromArgb(0xF2, 0x1C, 0x21, 0x29);
        private static readonly Color CardBottomColor = Color.FromArgb(0xF2, 0x0B, 0x0E, 0x13);
        private static readonly Color EdgeTopColor = Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF);
        private static readonly Color EdgeBottomColor = Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF);
        private static readonly Color GuColor = Color.FromRgb(0x32, 0xD7, 0x4B);
        private static readonly SolidColorBrush DimText = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));

        private readonly AppConfig _cfg;
        private readonly Grid _root;
        private readonly Border _card;
        private readonly TextBlock _dateText;
        private readonly TextBlock _timeText;
        private readonly TextBlock _nameText;
        private readonly TextBlock _balanceText;
        private readonly TextBlock _countdownLabel;
        private readonly TextBlock _countdownText;
        private readonly TextBlock _gear;
        private readonly Ellipse _dot;
        private readonly Run _timeMain;
        private readonly Run _timeSec;

        private DispatcherTimer _clockTimer;
        private DispatcherTimer _balanceTimer;
        private DispatcherTimer _saveTimer;
        private DispatcherTimer _desktopTimer;
        private bool _busy;
        private bool _lastFeng;
        private bool _nameInitialized;
        private SettingsWindow _settings;
        private ContextMenu _menu;
        private MenuItem _menuTopmost;
        private MenuItem _menuModeNewApi;
        private MenuItem _menuModeOfficial;
        private MenuItem _menuLock;
        private MenuItem _menuAutoStart;
        private ToolTip _tip;
        private System.Windows.Forms.NotifyIcon _tray;
        private System.Windows.Forms.ToolStripMenuItem _trayTopmost;
        private System.Windows.Forms.ToolStripMenuItem _trayModeOfficial;
        private System.Windows.Forms.ToolStripMenuItem _trayLock;
        private System.Windows.Forms.ToolStripMenuItem _trayAutoStart;
        private System.Windows.Forms.ToolStripMenuItem _trayToggle;

        public AppConfig Config { get { return _cfg; } }

        public WidgetWindow(AppConfig cfg) : this(cfg, false)
        {
        }

        public WidgetWindow(AppConfig cfg, bool preview)
        {
            _cfg = cfg;
            _root = (Grid)UiLoader.Load("Widget.xaml");
            _card = (Border)_root.FindName("Card");
            _dateText = (TextBlock)_root.FindName("DateText");
            _timeText = (TextBlock)_root.FindName("TimeText");
            _nameText = (TextBlock)_root.FindName("NameText");
            _balanceText = (TextBlock)_root.FindName("BalanceText");
            _countdownLabel = (TextBlock)_root.FindName("CountdownLabel");
            _countdownText = (TextBlock)_root.FindName("CountdownText");
            _gear = (TextBlock)_root.FindName("GearButton");
            _dot = (Ellipse)_root.FindName("StatusDot");

            _timeText.Inlines.Clear();
            _timeMain = new Run("");
            _timeSec = new Run("");
            _timeSec.Foreground = DimText;
            _timeText.Inlines.Add(_timeMain);
            _timeText.Inlines.Add(_timeSec);

            Title = "\u6881\u6587\u5CF0\u8C37";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            Topmost = _cfg.Topmost;
            ApplyCardOpacity(_cfg.WindowOpacity);

            Viewbox host = new Viewbox();
            host.Stretch = Stretch.Uniform;
            host.Child = _root;
            Content = host;

            ApplyScale(_cfg.Scale, false);
            RestorePosition();

            BuildMenu();
            _card.ContextMenu = _menu;
            _card.MouseLeftButtonDown += OnCardMouseDown;
            _card.MouseRightButtonUp += OnCardRightButtonUp;
            _card.MouseEnter += OnCardMouseEnter;
            _card.MouseLeave += OnCardMouseLeave;
            _gear.MouseLeftButtonDown += OnGearMouseDown;
            MouseWheel += OnMouseWheel;
            LocationChanged += OnLocationChanged;
            StateChanged += OnStateChanged;
            Closing += OnClosing;

            _tip = new ToolTip();
            _tip.Background = new SolidColorBrush(Color.FromArgb(0xF2, 0x10, 0x13, 0x18));
            _tip.Foreground = new SolidColorBrush(Color.FromArgb(0xE8, 0xFF, 0xFF, 0xFF));
            _tip.BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
            _tip.Padding = new Thickness(10, 7, 10, 7);
            _tip.FontSize = 11.5;
            _tip.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
            _tip.HasDropShadow = true;
            ToolTipService.SetShowDuration(_card, 60000);
            ToolTipService.SetInitialShowDelay(_card, 400);
            ToolTipService.SetToolTip(_card, _tip);

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += OnClockTick;
            if (!preview) _clockTimer.Start();

            _saveTimer = new DispatcherTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _saveTimer.Tick += OnSaveTick;

            if (!preview) CreateTray();
            if (!preview)
            {
                _desktopTimer = new DispatcherTimer();
                _desktopTimer.Interval = TimeSpan.FromMilliseconds(400);
                _desktopTimer.Tick += delegate { OnDesktopFocusTick(); };
                _desktopTimer.Start();
            }
            UpdateClock();
            if (!preview)
            {
                SetStatus(StatusKind.Loading, "");
                StartBalanceTimer();
                RefreshBalance();
            }
        }

        private void CreateTray()
        {
            try
            {
                _tray = new System.Windows.Forms.NotifyIcon();
                _tray.Text = "\u6881\u6587\u5CF0\u8C37 \u00B7 \u684C\u9762\u6446\u4EF6";
                try
                {
                    using (Stream s = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico"))
                    {
                        if (s != null) _tray.Icon = new System.Drawing.Icon(s);
                    }
                }
                catch { }
                if (_tray.Icon == null) _tray.Icon = System.Drawing.SystemIcons.Application;

                System.Windows.Forms.ContextMenuStrip menu = new System.Windows.Forms.ContextMenuStrip();
                menu.Renderer = new DarkMenuRenderer();
                menu.ShowImageMargin = false;
                menu.BackColor = System.Drawing.Color.FromArgb(16, 19, 24);
                menu.ForeColor = System.Drawing.Color.FromArgb(232, 255, 255, 255);

                _trayToggle = new System.Windows.Forms.ToolStripMenuItem("\u663E\u793A / \u9690\u85CF\u6446\u4EF6");
                _trayToggle.Click += delegate { ToggleVisibility(); };
                menu.Items.Add(_trayToggle);

                System.Windows.Forms.ToolStripMenuItem traySettings = new System.Windows.Forms.ToolStripMenuItem("\u8BBE\u7F6E\u2026");
                traySettings.Click += delegate { OpenSettings(); };
                menu.Items.Add(traySettings);

                System.Windows.Forms.ToolStripMenuItem trayRefresh = new System.Windows.Forms.ToolStripMenuItem("\u7ACB\u5373\u5237\u65B0\u4F59\u989D");
                trayRefresh.Click += delegate { RefreshBalance(); };
                menu.Items.Add(trayRefresh);

                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                _trayTopmost = new System.Windows.Forms.ToolStripMenuItem("\u7A97\u53E3\u7F6E\u9876");
                _trayModeOfficial = new System.Windows.Forms.ToolStripMenuItem("\u4F59\u989D\u6765\u6E90\uFF1A\u5B98\u65B9");
                _trayModeOfficial.CheckOnClick = true;
                _trayModeOfficial.Click += delegate
                {
                    SwitchBalanceMode(_trayModeOfficial.Checked ? BalanceClient.ModeOfficial : BalanceClient.ModeNewApi);
                };
                menu.Items.Add(_trayModeOfficial);

                _trayTopmost.CheckOnClick = true;
                _trayTopmost.Click += delegate
                {
                    _cfg.Topmost = _trayTopmost.Checked;
                    Topmost = _cfg.Topmost;
                    _cfg.Save();
                };
                menu.Items.Add(_trayTopmost);

                _trayLock = new System.Windows.Forms.ToolStripMenuItem("\u9501\u5B9A\u4F4D\u7F6E");
                _trayLock.CheckOnClick = true;
                _trayLock.Click += delegate
                {
                    _cfg.Locked = _trayLock.Checked;
                    _cfg.Save();
                };
                menu.Items.Add(_trayLock);

                _trayAutoStart = new System.Windows.Forms.ToolStripMenuItem("\u5F00\u673A\u81EA\u542F\u52A8");
                _trayAutoStart.CheckOnClick = true;
                _trayAutoStart.Click += delegate
                {
                    bool want = _trayAutoStart.Checked;
                    if (AutoStart.Apply(want) && AutoStart.IsEnabled() == want)
                    {
                        _cfg.AutoStart = want;
                        _cfg.Save();
                        Log.Write("\u5F00\u673A\u81EA\u542F\u52A8\uFF1A" + (want ? "\u5F00" : "\u5173"));
                        return;
                    }
                    _trayAutoStart.Checked = AutoStart.IsEnabled();
                    _cfg.AutoStart = _trayAutoStart.Checked;
                    _cfg.Save();
                    System.Windows.MessageBox.Show("\u5F00\u673A\u81EA\u542F\u52A8\u8BBE\u7F6E\u5931\u8D25\uFF1A\u65E0\u6CD5\u5199\u5165\u6CE8\u518C\u8868\u9879\u3002",
                        "\u6881\u6587\u5CF0\u8C37", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                };
                menu.Items.Add(_trayAutoStart);

                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

                System.Windows.Forms.ToolStripMenuItem trayExit = new System.Windows.Forms.ToolStripMenuItem("\u9000\u51FA");
                trayExit.Click += delegate { Close(); };
                menu.Items.Add(trayExit);

                menu.Opening += delegate
                {
                    _trayToggle.Text = IsVisible ? "\u9690\u85CF\u6446\u4EF6" : "\u663E\u793A\u6446\u4EF6";
                    _trayTopmost.Checked = _cfg.Topmost;
                    _trayModeOfficial.Checked = BalanceClient.IsOfficial(_cfg.BalanceMode);
                    _trayLock.Checked = _cfg.Locked;
                    _trayAutoStart.Checked = _cfg.AutoStart;
                };

                _tray.ContextMenuStrip = menu;
                _tray.DoubleClick += delegate { ShowWidget(); };
                _tray.Visible = true;
                Log.Write("\u6258\u76D8\u56FE\u6807\u5DF2\u521B\u5EFA");
            }
            catch (Exception ex)
            {
                Log.Write("\u6258\u76D8\u56FE\u6807\u521B\u5EFA\u5931\u8D25\uFF1A" + ex.Message);
            }
        }

        private void DisposeTray()
        {
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { }
        }

        private void ToggleVisibility()
        {
            if (IsVisible) Hide();
            else ShowWidget();
        }

        private void ShowWidget()
        {
            if (!IsVisible) Show();
            BringToFront();
        }

        // 摆件按普通窗口的方式工作：不置顶，其他程序打开时自然盖在它上面。
        // 只有被「显示桌面 / Win+D」最小化时，才需要把它拉回正常状态。
        private void OnStateChanged(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Minimized) return;
            try { WindowState = WindowState.Normal; }
            catch { }
        }

        // 回到桌面时，把摆件抬到普通窗口的最上层（不抢焦点、不置顶），
        // 免得被桌面美化软件之类画在桌面上的窗口挡住；一旦点开别的软件，
        // 那个软件仍然会盖在摆件上面。
        private void OnDesktopFocusTick()
        {
            if (_cfg.Topmost || !IsVisible) return;
            try
            {
                if (!DesktopFocus.DesktopIsForeground()) return;
                IntPtr handle = new WindowInteropHelper(this).Handle;
                DesktopFocus.RaiseAboveNormalWindows(handle);
            }
            catch { }
        }

        private void BuildMenu()
        {
            _menu = new ContextMenu();
            _menu.Style = (Style)_root.Resources["DarkMenu"];

            MenuItem settings = new MenuItem();
            settings.Header = "\u8BBE\u7F6E\u2026";
            settings.Click += delegate { OpenSettings(); };
            ApplyItemStyle(settings);
            _menu.Items.Add(settings);

            MenuItem refresh = new MenuItem();
            refresh.Header = "\u7ACB\u5373\u5237\u65B0\u4F59\u989D";
            refresh.Click += delegate { RefreshBalance(); };
            ApplyItemStyle(refresh);
            _menu.Items.Add(refresh);

            _menu.Items.Add(MakeSeparator());

            _menuTopmost = new MenuItem();
            _menuModeNewApi = new MenuItem();
            _menuModeNewApi.Header = "\u4F59\u989D\u6765\u6E90\uFF1ANewAPI \u4E2D\u8F6C\u7AD9";
            _menuModeNewApi.IsCheckable = true;
            _menuModeNewApi.Click += delegate { SwitchBalanceMode(BalanceClient.ModeNewApi); };
            ApplyItemStyle(_menuModeNewApi);
            _menu.Items.Add(_menuModeNewApi);

            _menuModeOfficial = new MenuItem();
            _menuModeOfficial.Header = "\u4F59\u989D\u6765\u6E90\uFF1A\u5B98\u65B9";
            _menuModeOfficial.IsCheckable = true;
            _menuModeOfficial.Click += delegate { SwitchBalanceMode(BalanceClient.ModeOfficial); };
            ApplyItemStyle(_menuModeOfficial);
            _menu.Items.Add(_menuModeOfficial);

            _menu.Items.Add(MakeSeparator());

            _menuTopmost.Header = "\u7A97\u53E3\u7F6E\u9876";
            _menuTopmost.IsCheckable = true;
            _menuTopmost.Click += delegate
            {
                _cfg.Topmost = _menuTopmost.IsChecked;
                Topmost = _cfg.Topmost;
                _cfg.Save();
            };
            ApplyItemStyle(_menuTopmost);
            _menu.Items.Add(_menuTopmost);

            _menuLock = new MenuItem();
            _menuLock.Header = "\u9501\u5B9A\u4F4D\u7F6E";
            _menuLock.IsCheckable = true;
            _menuLock.Click += delegate
            {
                _cfg.Locked = _menuLock.IsChecked;
                _cfg.Save();
            };
            ApplyItemStyle(_menuLock);
            _menu.Items.Add(_menuLock);

            _menuAutoStart = new MenuItem();
            _menuAutoStart.Header = "\u5F00\u673A\u81EA\u542F\u52A8";
            _menuAutoStart.IsCheckable = true;
            _menuAutoStart.Click += delegate
            {
                bool want = _menuAutoStart.IsChecked;
                if (AutoStart.Apply(want) && AutoStart.IsEnabled() == want)
                {
                    _cfg.AutoStart = want;
                    _cfg.Save();
                    return;
                }
                _menuAutoStart.IsChecked = AutoStart.IsEnabled();
                _cfg.AutoStart = _menuAutoStart.IsChecked;
                _cfg.Save();
                MessageBox.Show("\u5F00\u673A\u81EA\u542F\u52A8\u8BBE\u7F6E\u5931\u8D25\uFF1A\u65E0\u6CD5\u5199\u5165\u6CE8\u518C\u8868\u9879\u3002",
                    "\u6881\u6587\u5CF0\u8C37", MessageBoxButton.OK, MessageBoxImage.Warning);
            };
            ApplyItemStyle(_menuAutoStart);
            _menu.Items.Add(_menuAutoStart);

            _menu.Items.Add(MakeSeparator());

            MenuItem exit = new MenuItem();
            exit.Header = "\u9000\u51FA";
            exit.Click += delegate { Close(); };
            ApplyItemStyle(exit);
            _menu.Items.Add(exit);

            _menu.Opened += delegate
            {
                _menuTopmost.IsChecked = _cfg.Topmost;
                _menuModeNewApi.IsChecked = !BalanceClient.IsOfficial(_cfg.BalanceMode);
                _menuModeOfficial.IsChecked = BalanceClient.IsOfficial(_cfg.BalanceMode);
                _menuLock.IsChecked = _cfg.Locked;
                _menuAutoStart.IsChecked = _cfg.AutoStart;
            };
        }

        private void ApplyItemStyle(MenuItem item)
        {
            item.Style = (Style)_root.Resources["DarkMenuItem"];
        }

        private Separator MakeSeparator()
        {
            Separator s = new Separator();
            s.Style = (Style)_root.Resources["DarkSeparator"];
            return s;
        }

        public void ApplyScale(double scale, bool keepCenter)
        {
            _cfg.Scale = scale;
            double w = DesignWidth * scale;
            double h = DesignHeight * scale;
            if (keepCenter && IsLoaded)
            {
                double cx = Left + Width / 2.0;
                double cy = Top + Height / 2.0;
                Width = w;
                Height = h;
                Left = cx - w / 2.0;
                Top = cy - h / 2.0;
            }
            else
            {
                Width = w;
                Height = h;
            }
        }

        private void RestorePosition()
        {
            if (_cfg.HasPosition)
            {
                Left = _cfg.Left;
                Top = _cfg.Top;
            }
            else
            {
                Rect wa = SystemParameters.WorkArea;
                Left = wa.Right - Width - 40;
                Top = wa.Bottom - Height - 40;
            }
            EnsureOnScreen();
        }

        private void ClampToScreen()
        {
            try
            {
                System.Drawing.Rectangle area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top)).WorkingArea;
                if (Left + Width > area.Right) Left = area.Right - Width;
                if (Top + Height > area.Bottom) Top = area.Bottom - Height;
                if (Left < area.Left) Left = area.Left;
                if (Top < area.Top) Top = area.Top;
            }
            catch { }
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (!IsLoaded) return;
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void OnSaveTick(object sender, EventArgs e)
        {
            _saveTimer.Stop();
            SavePosition();
        }

        public void SavePosition()
        {
            _cfg.Left = Left;
            _cfg.Top = Top;
            _cfg.HasPosition = true;
            _cfg.Save();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_desktopTimer != null) _desktopTimer.Stop();
            SavePosition();
            Log.Write("\u5173\u95ED\u6446\u4EF6\uFF0C\u4FDD\u5B58\u4F4D\u7F6E Left=" + Left.ToString("0.#", CultureInfo.InvariantCulture) + " Top=" + Top.ToString("0.#", CultureInfo.InvariantCulture));
            DisposeTray();
            if (_settings != null)
            {
                _settings.ForceClose();
                _settings = null;
            }
            if (Application.Current != null) Application.Current.Shutdown();
        }

        private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_cfg.Locked) return;
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch { }
            }
        }

        private void OnCardRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_menu != null)
            {
                _menu.PlacementTarget = _card;
                _menu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void OnCardMouseEnter(object sender, MouseEventArgs e)
        {
            AnimateOpacity(_gear, 0.85, 140);
        }

        private void OnCardMouseLeave(object sender, MouseEventArgs e)
        {
            AnimateOpacity(_gear, 0.0, 220);
        }

        private static void AnimateOpacity(UIElement el, double to, int ms)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            el.BeginAnimation(UIElement.OpacityProperty, a);
        }

        private void OnGearMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            OpenSettings();
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                double opacityStep = e.Delta > 0 ? 0.05 : -0.05;
                double nextOpacity = Math.Round((_cfg.WindowOpacity + opacityStep) * 100.0) / 100.0;
                if (nextOpacity < 0.3) nextOpacity = 0.3;
                if (nextOpacity > 1.0) nextOpacity = 1.0;
                if (Math.Abs(nextOpacity - _cfg.WindowOpacity) >= 0.001)
                {
                    ApplyCardOpacity(nextOpacity);
                    _cfg.WindowOpacity = nextOpacity;
                    _cfg.Save();
                    if (_settings != null) _settings.SyncFromConfig();
                }
                e.Handled = true;
                return;
            }
            double step = e.Delta > 0 ? 0.05 : -0.05;
            double next = Math.Round((_cfg.Scale + step) * 100.0) / 100.0;
            if (next < 0.6) next = 0.6;
            if (next > 2.0) next = 2.0;
            if (Math.Abs(next - _cfg.Scale) < 0.001) return;
            ApplyScale(next, true);
            ClampToScreen();
            _cfg.Save();
            if (_settings != null) _settings.SyncFromConfig();
            e.Handled = true;
        }

        public void OpenSettings()
        {
            if (_settings != null)
            {
                _settings.Activate();
                return;
            }
            _settings = new SettingsWindow(this);
            _settings.Closed += delegate { _settings = null; };
            _settings.Show();
        }

        public void BringToFront()
        {
            try
            {
                if (!IsVisible) Show();
                if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
                EnsureOnScreen();
                bool restore = !_cfg.Topmost;
                if (restore) Topmost = true;
                Activate();
                Pulse();
                if (restore)
                {
                    DispatcherTimer timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(1800);
                    timer.Tick += delegate
                    {
                        timer.Stop();
                        Topmost = _cfg.Topmost;
                    };
                    timer.Start();
                }
            }
            catch { }
        }

        private void EnsureOnScreen()
        {
            try
            {
                System.Drawing.Rectangle self = new System.Drawing.Rectangle(
                    (int)Math.Round(Left), (int)Math.Round(Top),
                    (int)Math.Round(Width), (int)Math.Round(Height));
                bool onScreen = false;
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                {
                    System.Drawing.Rectangle overlap = System.Drawing.Rectangle.Intersect(self, screen.WorkingArea);
                    if (overlap.Width >= 80 && overlap.Height >= 80)
                    {
                        onScreen = true;
                        break;
                    }
                }
                if (!onScreen)
                {
                    System.Drawing.Rectangle wa = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;
                    Left = wa.Right - Width - 40;
                    Top = wa.Bottom - Height - 40;
                }
                ClampToScreen();
            }
            catch { }
        }

        private void Pulse()
        {
            DoubleAnimationUsingKeyFrames anim = new DoubleAnimationUsingKeyFrames();
            anim.Duration = TimeSpan.FromMilliseconds(1300);
            anim.FillBehavior = FillBehavior.Stop;
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300))));
            _card.Opacity = 1.0;
            _card.BeginAnimation(UIElement.OpacityProperty, anim);
        }
        private void OnClockTick(object sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            UpdateClockAt(ClockService.Now());
        }

        public void UpdateClockAt(DateTime now)
        {
            _dateText.Text = string.Format(CultureInfo.InvariantCulture, "{0}\u5E74{1}\u6708{2}\u65E5 {3}",
                now.Year, now.Month, now.Day, ClockService.WeekdayCn(now));
            _timeMain.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            _timeSec.Text = now.ToString(":ss", CultureInfo.InvariantCulture);

            bool feng = ClockService.IsFeng(now);
            if (!_nameInitialized || feng != _lastFeng)
            {
                _nameInitialized = true;
                _lastFeng = feng;
                ApplyName(feng);
            }
            ApplyCountdown(now);
        }

        private void ApplyCountdown(DateTime now)
        {
            DateTime next = ClockService.NextSwitch(now);
            bool feng = ClockService.IsFeng(now);
            _countdownLabel.Text = feng ? "距梁文谷" : "距梁文峰";
            _countdownText.Text = ClockService.FormatCountdown(next - now);
            Color c = feng ? GuColor : FengColor;
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(0xE6, c.R, c.G, c.B));
            brush.Freeze();
            _countdownText.Foreground = brush;
        }

        private void ApplyName(bool feng)
        {
            Color c = feng ? FengColor : GuColor;
            SolidColorBrush brush = new SolidColorBrush(c);
            brush.Freeze();
            _nameText.Text = feng ? "\u6881\u6587\u5CF0" : "\u6881\u6587\u8C37";
            _nameText.Foreground = brush;
            DropShadowEffect glow = new DropShadowEffect();
            glow.Color = c;
            glow.BlurRadius = 16;
            glow.ShadowDepth = 0;
            glow.Opacity = 0.42;
            _nameText.Effect = glow;
        }

        private void StartBalanceTimer()
        {
            if (_balanceTimer != null) _balanceTimer.Stop();
            _balanceTimer = new DispatcherTimer();
            int minutes = _cfg.RefreshMinutes < 1 ? 1 : _cfg.RefreshMinutes;
            _balanceTimer.Interval = TimeSpan.FromMinutes(minutes);
            _balanceTimer.Tick += delegate { RefreshBalance(); };
            _balanceTimer.Start();
        }

        public void RefreshBalance()
        {
            if (_busy) return;
            _busy = true;
            SetStatus(StatusKind.Loading, "");
            AppConfig snapshot = _cfg.Clone();
            ThreadPool.QueueUserWorkItem(delegate
            {
                BalanceResult result = BalanceClient.Query(snapshot);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    ApplyBalance(result);
                }));
            });
        }

        public string DebugState()
        {
            SolidColorBrush brush = _nameText.Foreground as SolidColorBrush;
            string color = brush == null ? "?" : brush.Color.ToString();
            return _nameText.Text + "|" + color + "|" + _countdownLabel.Text + " " + _countdownText.Text;
        }

        public Grid TakeRoot()
        {
            Viewbox vb = Content as Viewbox;
            if (vb != null) vb.Child = null;
            return _root;
        }

        public void SetPreviewSample()
        {
            _dateText.Text = "2026\u5E749\u670817\u65E5 \u661F\u671F\u56DB";
            _timeMain.Text = "14:23";
            _timeSec.Text = ":05";
            _nameInitialized = true;
            _lastFeng = true;
            ApplyName(true);
            SetStatus(StatusKind.Ok, "");
            _balanceText.Text = "\u00A512.34";
            _balanceText.Foreground = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
            _countdownLabel.Text = "\u8DDD\u6881\u6587\u8C37";
            _countdownText.Text = "01:36:22";
            _countdownText.Foreground = new SolidColorBrush(Color.FromArgb(0xE6, 0x32, 0xD7, 0x4B));
            _gear.Opacity = 0.85;
        }

        private enum StatusKind { Loading, Ok, Error }

        private void SetStatus(StatusKind kind, string message)
        {
            switch (kind)
            {
                case StatusKind.Loading:
                    _dot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A));
                    _balanceText.Text = "\u2026";
                    _balanceText.Foreground = new SolidColorBrush(Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF));
                    _tip.Content = message.Length > 0 ? message : "\u6B63\u5728\u83B7\u53D6\u4F59\u989D\u2026";
                    break;
                case StatusKind.Ok:
                    _dot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x32, 0xD7, 0x4B));
                    break;
                case StatusKind.Error:
                    _dot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xB0, 0x20));
                    _balanceText.Text = "--";
                    _balanceText.Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF));
                    _tip.Content = message;
                    break;
            }
        }

        private void ApplyBalance(BalanceResult result)
        {
            if (result.Ok)
            {
                SetStatus(StatusKind.Ok, "");
                string value = SymbolOf(result) + result.Remaining.ToString("N2", CultureInfo.InvariantCulture);
                _balanceText.Text = value;
                _balanceText.Foreground = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
                _tip.Content = BuildTooltip(result);
                Log.Write("\u4F59\u989D\u66F4\u65B0\u6210\u529F\uFF1A" + value);
            }
            else
            {
                SetStatus(StatusKind.Error, "\u4F59\u989D\u83B7\u53D6\u5931\u8D25\uFF1A" + result.Error);
                Log.Write("\u4F59\u989D\u83B7\u53D6\u5931\u8D25\uFF1A" + result.Error + "  " + result.RawEndpoint);
            }
        }

        private string BuildTooltip(BalanceResult r)
        {
            DateTime now = ClockService.Now();
            string symbol = SymbolOf(r);
            string text = "\u4F59\u989D\uFF1A" + symbol + r.Remaining.ToString("N2", CultureInfo.InvariantCulture);
            if (r.Official)
            {
                if (r.PlanName.Length > 0) text += "\n\u6765\u6E90\uFF1A" + r.PlanName;
                if (r.Extra.Length > 0) text += "\n" + r.Extra;
                if (r.Total > 0)
                {
                    text += "\n\u5DF2\u7528\uFF1A" + symbol + r.Used.ToString("N2", CultureInfo.InvariantCulture);
                    text += "\n\u603B\u989D\uFF1A" + symbol + r.Total.ToString("N2", CultureInfo.InvariantCulture);
                }
            }
            else
            {
                if (r.PlanName.Length > 0) text += "\n\u5957\u9910\uFF1A" + r.PlanName;
                text += "\n\u5DF2\u7528\uFF1A" + symbol + r.Used.ToString("N2", CultureInfo.InvariantCulture);
                text += "\n\u603B\u989D\uFF1A" + symbol + r.Total.ToString("N2", CultureInfo.InvariantCulture);
            }
            text += "\n\u66F4\u65B0\uFF1A" + now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            text += "\n" + ClockService.PeriodHint(now) + " \u00B7 " + (ClockService.IsFeng(now) ? "\u6881\u6587\u5CF0" : "\u6881\u6587\u8C37");
            text += "\n" + (ClockService.IsFeng(now) ? "\u8DDD\u6881\u6587\u8C37 " : "\u8DDD\u6881\u6587\u5CF0 ") + ClockService.FormatCountdown(ClockService.NextSwitch(now) - now);
            return text;
        }

        public void ApplyFromConfig(AppConfig cfg)
        {
            _cfg.BaseUrl = cfg.BaseUrl;
            _cfg.AccessToken = cfg.AccessToken;
            _cfg.UserId = cfg.UserId;
            _cfg.CurrencySymbol = cfg.CurrencySymbol;
            _cfg.BalanceMode = cfg.BalanceMode;
            _cfg.OfficialBaseUrl = cfg.OfficialBaseUrl;
            _cfg.OfficialApiKey = cfg.OfficialApiKey;
            _cfg.QuotaPerUnit = cfg.QuotaPerUnit;
            _cfg.RefreshMinutes = cfg.RefreshMinutes;
            _cfg.Topmost = cfg.Topmost;
            _cfg.Locked = cfg.Locked;
            _cfg.AutoStart = cfg.AutoStart;
            _cfg.WindowOpacity = cfg.WindowOpacity;
            _cfg.Scale = cfg.Scale;
            _cfg.Normalize();
            _cfg.Save();

            Topmost = _cfg.Topmost;
            ApplyCardOpacity(_cfg.WindowOpacity);
            ApplyScale(_cfg.Scale, true);
            ClampToScreen();
            StartBalanceTimer();
            RefreshBalance();
        }

        public void ApplyVisual(double scale, double opacity, bool topmost, bool locked)
        {
            ApplyScale(scale, true);
            ApplyCardOpacity(opacity);
            Topmost = topmost;
            _cfg.Locked = locked;
        }

        public void SwitchBalanceMode(string mode)
        {
            _cfg.BalanceMode = BalanceClient.NormalizeMode(mode);
            _cfg.Save();
            Log.Write("\u4F59\u989D\u6765\u6E90\u5207\u6362\u4E3A\uFF1A" + (BalanceClient.IsOfficial(_cfg.BalanceMode) ? "\u5B98\u65B9" : "NewAPI \u4E2D\u8F6C\u7AD9"));
            SetStatus(StatusKind.Loading, "");
            RefreshBalance();
        }

        private string SymbolOf(BalanceResult r)
        {
            if (r.Symbol != null && r.Symbol.Length > 0) return r.Symbol;
            return _cfg.CurrencySymbol;
        }

        // \u4E0D\u900F\u660E\u5EA6\u53EA\u4F5C\u7528\u4E8E\u5361\u7247\u80CC\u666F\uFF08\u542B\u63CF\u8FB9\u3001\u6295\u5F71\uFF09\uFF0C\u6587\u5B57\u59CB\u7EC8\u4FDD\u6301\u4E0D\u900F\u660E\u3002
        private void ApplyCardOpacity(double opacity)
        {
            if (opacity < 0.05) opacity = 0.05;
            if (opacity > 1.0) opacity = 1.0;
            _card.Background = GradientBrush(CardTopColor, CardBottomColor, opacity, 0.45, 1);
            _card.BorderBrush = GradientBrush(EdgeTopColor, EdgeBottomColor, opacity, 0, 1);
            DropShadowEffect shadow = _card.Effect as DropShadowEffect;
            if (shadow != null)
            {
                DropShadowEffect copy = shadow.Clone();
                copy.Opacity = 0.5 * opacity;
                _card.Effect = copy;
            }
        }

        private static Brush GradientBrush(Color top, Color bottom, double factor, double endX, double endY)
        {
            LinearGradientBrush brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(endX, endY);
            brush.GradientStops.Add(new GradientStop(ScaleAlpha(top, factor), 0));
            brush.GradientStops.Add(new GradientStop(ScaleAlpha(bottom, factor), 1));
            brush.Freeze();
            return brush;
        }

        private static Color ScaleAlpha(Color c, double factor)
        {
            int a = (int)Math.Round(c.A * factor);
            if (a < 0) a = 0;
            if (a > 255) a = 255;
            return Color.FromArgb((byte)a, c.R, c.G, c.B);
        }
    }

    public static class UiLoader
    {
        public static object Load(string name)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("\u7F3A\u5C11\u8D44\u6E90\uFF1A" + name);
                return XamlReader.Load(s);
            }
        }
    }
}
