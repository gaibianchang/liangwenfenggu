using System;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LiangWenFengGu
{
    public class SettingsWindow : Window
    {
        private readonly WidgetWindow _widget;
        private readonly AppConfig _original;
        private readonly Grid _root;
        private readonly Border _card;
        private readonly TextBox _baseUrlBox;
        private readonly TextBox _tokenBox;
        private readonly TextBox _userIdBox;
        private readonly TextBox _quotaBox;
        private readonly TextBox _refreshBox;
        private readonly Button _modeNewApiButton;
        private readonly Button _modeOfficialButton;
        private readonly StackPanel _newApiPanel;
        private readonly StackPanel _officialPanel;
        private readonly TextBlock _modeHint;
        private readonly TextBox _officialUrlBox;
        private readonly TextBox _officialKeyBox;
        private readonly Slider _scaleSlider;
        private readonly Slider _opacitySlider;
        private readonly TextBlock _scaleValue;
        private readonly TextBlock _opacityValue;
        private readonly CheckBox _topmostCheck;
        private readonly CheckBox _lockCheck;
        private readonly CheckBox _autoStartCheck;
        private readonly Border _statusPanel;
        private readonly TextBlock _statusText;
        private readonly Button _testButton;
        private bool _busy;
        private bool _forceClosed;
        private string _mode;

        public SettingsWindow(WidgetWindow widget)
        {
            _widget = widget;
            _original = widget.Config.Clone();

            _root = (Grid)UiLoader.Load("Settings.xaml");
            _card = (Border)_root.Children[0];
            _baseUrlBox = (TextBox)_root.FindName("BaseUrlBox");
            _tokenBox = (TextBox)_root.FindName("TokenBox");
            _userIdBox = (TextBox)_root.FindName("UserIdBox");
            _quotaBox = (TextBox)_root.FindName("QuotaBox");
            _refreshBox = (TextBox)_root.FindName("RefreshBox");
            _modeNewApiButton = (Button)_root.FindName("ModeNewApiButton");
            _modeOfficialButton = (Button)_root.FindName("ModeOfficialButton");
            _newApiPanel = (StackPanel)_root.FindName("NewApiPanel");
            _officialPanel = (StackPanel)_root.FindName("OfficialPanel");
            _modeHint = (TextBlock)_root.FindName("ModeHint");
            _officialUrlBox = (TextBox)_root.FindName("OfficialUrlBox");
            _officialKeyBox = (TextBox)_root.FindName("OfficialKeyBox");
            _scaleSlider = (Slider)_root.FindName("ScaleSlider");
            _opacitySlider = (Slider)_root.FindName("OpacitySlider");
            _scaleValue = (TextBlock)_root.FindName("ScaleValue");
            _opacityValue = (TextBlock)_root.FindName("OpacityValue");
            _topmostCheck = (CheckBox)_root.FindName("TopmostCheck");
            _lockCheck = (CheckBox)_root.FindName("LockCheck");
            _autoStartCheck = (CheckBox)_root.FindName("AutoStartCheck");
            _statusPanel = (Border)_root.FindName("StatusPanel");
            _statusText = (TextBlock)_root.FindName("StatusText");
            _testButton = (Button)_root.FindName("TestButton");

            Title = "\u8BBE\u7F6E";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.Height;
            Width = 470;
            Topmost = true;
            SnapsToDevicePixels = true;
            Content = _root;

            AppConfig cfg = _widget.Config;
            _baseUrlBox.Text = cfg.BaseUrl;
            _tokenBox.Text = cfg.AccessToken;
            _userIdBox.Text = cfg.UserId;
            _quotaBox.Text = cfg.QuotaPerUnit.ToString("0", CultureInfo.InvariantCulture);
            _refreshBox.Text = cfg.RefreshMinutes.ToString(CultureInfo.InvariantCulture);
            _officialUrlBox.Text = cfg.OfficialBaseUrl;
            _officialKeyBox.Text = cfg.OfficialApiKey;
            _mode = BalanceClient.NormalizeMode(cfg.BalanceMode);
            _scaleSlider.Value = cfg.Scale;
            _opacitySlider.Value = cfg.WindowOpacity;
            _topmostCheck.IsChecked = cfg.Topmost;
            _lockCheck.IsChecked = cfg.Locked;
            _autoStartCheck.IsChecked = cfg.AutoStart;
            UpdateValueLabels();

            ((Grid)_root.FindName("TitleBar")).MouseLeftButtonDown += OnTitleBarMouseDown;
            ((Button)_root.FindName("CloseButton")).Click += delegate { Cancel(); };
            ((Button)_root.FindName("CancelButton")).Click += delegate { Cancel(); };
            ((Button)_root.FindName("SaveButton")).Click += delegate { Save(); };
            _testButton.Click += delegate { RunTest(); };
            _scaleSlider.ValueChanged += delegate { LivePreview(); };
            _opacitySlider.ValueChanged += delegate { LivePreview(); };
            _topmostCheck.Click += delegate { LivePreview(); };
            _lockCheck.Click += delegate { LivePreview(); };
            _modeNewApiButton.Click += delegate { SetMode(BalanceClient.ModeNewApi); };
            _modeOfficialButton.Click += delegate { SetMode(BalanceClient.ModeOfficial); };
            UpdateModeUi();
            KeyDown += OnKeyDown;
            Closing += OnClosing;

            PositionNearWidget();
        }

        private void PositionNearWidget()
        {
            try
            {
                double w = 470;
                double h = 640;
                double left = _widget.Left + _widget.Width / 2.0 - w / 2.0;
                double top = _widget.Top + _widget.Height + 12;
                System.Drawing.Rectangle area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)_widget.Left, (int)_widget.Top)).WorkingArea;
                if (left < area.Left) left = area.Left + 12;
                if (left + w > area.Right) left = area.Right - w - 12;
                if (top + h > area.Bottom) top = _widget.Top - h - 12;
                if (top < area.Top) top = area.Top + 12;
                Left = left;
                Top = top;
            }
            catch
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); }
                catch { }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Cancel();
            else if (e.Key == Key.Enter && e.KeyboardDevice.Modifiers == ModifierKeys.Control) Save();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceClosed) Revert();
        }

        public void ForceClose()
        {
            _forceClosed = true;
            Close();
        }

        private void SetMode(string mode)
        {
            _mode = BalanceClient.NormalizeMode(mode);
            UpdateModeUi();
        }

        private void UpdateModeUi()
        {
            bool official = BalanceClient.IsOfficial(_mode);
            _modeNewApiButton.Style = (Style)_root.Resources[official ? "BtnGhost" : "BtnPrimary"];
            _modeOfficialButton.Style = (Style)_root.Resources[official ? "BtnPrimary" : "BtnGhost"];
            _newApiPanel.Visibility = official ? Visibility.Collapsed : Visibility.Visible;
            _officialPanel.Visibility = official ? Visibility.Visible : Visibility.Collapsed;
            _modeHint.Text = official
                ? "\u67E5\u8BE2\u5B98\u65B9\u5E10\u53F7\u4F59\u989D\uFF08DeepSeek / SiliconFlow / OpenRouter / Novita\uFF09"
                : "\u67E5\u8BE2\u4E2D\u8F6C\u7AD9\u8D26\u6237\u4F59\u989D\uFF08GET /api/user/self\uFF09";
        }

        private void UpdateValueLabels()
        {
            _scaleValue.Text = _scaleSlider.Value.ToString("0.00", CultureInfo.InvariantCulture) + "x";
            _opacityValue.Text = ((int)Math.Round(_opacitySlider.Value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void LivePreview()
        {
            UpdateValueLabels();
            _widget.ApplyVisual(_scaleSlider.Value, _opacitySlider.Value,
                _topmostCheck.IsChecked == true, _lockCheck.IsChecked == true);
        }

        public void SyncFromConfig()
        {
            _scaleSlider.Value = _widget.Config.Scale;
            _opacitySlider.Value = _widget.Config.WindowOpacity;
            UpdateValueLabels();
        }

        private void Revert()
        {
            _widget.ApplyVisual(_original.Scale, _original.WindowOpacity, _original.Topmost, _original.Locked);
            _widget.Config.Locked = _original.Locked;
        }

        private void Cancel()
        {
            Revert();
            _forceClosed = true;
            Close();
        }

        private void Save()
        {
            AppConfig cfg = _original.Clone();
            cfg.BaseUrl = _baseUrlBox.Text.Trim();
            cfg.AccessToken = _tokenBox.Text.Trim();
            cfg.UserId = _userIdBox.Text.Trim();
            cfg.BalanceMode = _mode;
            cfg.OfficialBaseUrl = _officialUrlBox.Text.Trim();
            cfg.OfficialApiKey = _officialKeyBox.Text.Trim();
            cfg.CurrencySymbol = _original.CurrencySymbol;
            double quota;
            if (double.TryParse(_quotaBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out quota) && quota > 0)
            {
                cfg.QuotaPerUnit = quota;
            }
            int minutes;
            if (int.TryParse(_refreshBox.Text.Trim(), out minutes) && minutes >= 1)
            {
                cfg.RefreshMinutes = minutes;
            }
            cfg.Scale = _scaleSlider.Value;
            cfg.WindowOpacity = _opacitySlider.Value;
            cfg.Topmost = _topmostCheck.IsChecked == true;
            cfg.Locked = _lockCheck.IsChecked == true;
            cfg.AutoStart = _autoStartCheck.IsChecked == true;
            if (AutoStart.Apply(cfg.AutoStart) && AutoStart.IsEnabled() == cfg.AutoStart)
            {
                _forceClosed = true;
                _widget.ApplyFromConfig(cfg);
                Close();
                return;
            }
            _autoStartCheck.IsChecked = AutoStart.IsEnabled();
            SetStatus("\u5F00\u673A\u81EA\u542F\u52A8\u8BBE\u7F6E\u5931\u8D25\uFF1A\u65E0\u6CD5\u5199\u5165\u6CE8\u518C\u8868\u9879\u3002\n\u8BF7\u5C1D\u8BD5\u4EE5\u7BA1\u7406\u5458\u8EAB\u4EFD\u8FD0\u884C\uFF0C\u6216\u624B\u52A8\u6DFB\u52A0\u3002",
                Color.FromArgb(0xFF, 0xFF, 0x9F, 0x5A));
        }

        private void SetStatus(string text, Color color)
        {
            _statusPanel.Visibility = Visibility.Visible;
            _statusText.Text = text;
            _statusText.Foreground = new SolidColorBrush(color);
        }

        private void RunTest()
        {
            if (_busy) return;
            _busy = true;
            _testButton.IsEnabled = false;
            SetStatus("\u6B63\u5728\u6D4B\u8BD5\u8FDE\u63A5\u2026", Color.FromArgb(0xB3, 0xFF, 0xFF, 0xFF));

            AppConfig probe = _original.Clone();
            probe.BaseUrl = _baseUrlBox.Text.Trim();
            probe.AccessToken = _tokenBox.Text.Trim();
            probe.UserId = _userIdBox.Text.Trim();
            probe.BalanceMode = _mode;
            probe.OfficialBaseUrl = _officialUrlBox.Text.Trim();
            probe.OfficialApiKey = _officialKeyBox.Text.Trim();
            double quota;
            if (double.TryParse(_quotaBox.Text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out quota) && quota > 0)
            {
                probe.QuotaPerUnit = quota;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                BalanceResult r = BalanceClient.Query(probe);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    _testButton.IsEnabled = true;
                    if (r.Ok)
                    {
                        string text = "\u8FDE\u63A5\u6210\u529F \u00B7 \u5F53\u524D\u4F59\u989D " + probe.CurrencySymbol +
                            r.Remaining.ToString("N2", CultureInfo.InvariantCulture);
                        if (r.PlanName.Length > 0) text += "\uFF08" + r.PlanName + "\uFF09";
                        text += "\n\u5DF2\u7528 " + probe.CurrencySymbol + r.Used.ToString("N2", CultureInfo.InvariantCulture) +
                            " \u00B7 \u603B\u989D " + probe.CurrencySymbol + r.Total.ToString("N2", CultureInfo.InvariantCulture);
                        text += "\n" + r.RawEndpoint;
                        SetStatus(text, Color.FromArgb(0xFF, 0x6E, 0xD9, 0x8A));
                    }
                    else
                    {
                        SetStatus("\u6D4B\u8BD5\u5931\u8D25\uFF1A" + r.Error + "\n" + r.RawEndpoint, Color.FromArgb(0xFF, 0xFF, 0x9F, 0x5A));
                    }
                }));
            });
        }
    }
}
