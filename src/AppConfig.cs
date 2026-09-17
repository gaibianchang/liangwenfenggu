using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LiangWenFengGu
{
    public class AppConfig
    {
        public string BaseUrl { get; set; }
        public string AccessToken { get; set; }
        public string UserId { get; set; }
        public string CurrencySymbol { get; set; }
        public string BalanceMode { get; set; }
        public string OfficialBaseUrl { get; set; }
        public string OfficialApiKey { get; set; }
        public double QuotaPerUnit { get; set; }
        public int RefreshMinutes { get; set; }
        public double Scale { get; set; }
        public double WindowOpacity { get; set; }
        public bool Topmost { get; set; }
        public bool Locked { get; set; }
        public bool AutoStart { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool HasPosition { get; set; }

        private static string _path;

        public AppConfig()
        {
            BaseUrl = "";
            AccessToken = "";
            UserId = "";
            CurrencySymbol = "\u00A5";
            BalanceMode = BalanceClient.ModeNewApi;
            OfficialBaseUrl = "https://api.deepseek.com";
            OfficialApiKey = "";
            QuotaPerUnit = 500000;
            RefreshMinutes = 5;
            Scale = 1.0;
            WindowOpacity = 1.0;
            Topmost = false;
            Locked = false;
            AutoStart = false;
            Left = 0;
            Top = 0;
            HasPosition = false;
        }

        public AppConfig Clone()
        {
            AppConfig c = new AppConfig();
            c.BaseUrl = BaseUrl;
            c.AccessToken = AccessToken;
            c.UserId = UserId;
            c.CurrencySymbol = CurrencySymbol;
            c.BalanceMode = BalanceMode;
            c.OfficialBaseUrl = OfficialBaseUrl;
            c.OfficialApiKey = OfficialApiKey;
            c.QuotaPerUnit = QuotaPerUnit;
            c.RefreshMinutes = RefreshMinutes;
            c.Scale = Scale;
            c.WindowOpacity = WindowOpacity;
            c.Topmost = Topmost;
            c.Locked = Locked;
            c.AutoStart = AutoStart;
            c.Left = Left;
            c.Top = Top;
            c.HasPosition = HasPosition;
            return c;
        }

        public void Normalize()
        {
            if (BaseUrl == null) BaseUrl = "";
            BaseUrl = BaseUrl.Trim();
            if (AccessToken == null) AccessToken = "";
            AccessToken = AccessToken.Trim();
            if (UserId == null) UserId = "";
            UserId = UserId.Trim();
            if (string.IsNullOrEmpty(CurrencySymbol)) CurrencySymbol = "\u00A5";
            BalanceMode = BalanceClient.NormalizeMode(BalanceMode);
            if (OfficialBaseUrl == null) OfficialBaseUrl = "";
            OfficialBaseUrl = OfficialBaseUrl.Trim();
            if (OfficialBaseUrl.Length == 0) OfficialBaseUrl = "https://api.deepseek.com";
            if (OfficialApiKey == null) OfficialApiKey = "";
            OfficialApiKey = OfficialApiKey.Trim();
            if (QuotaPerUnit <= 0) QuotaPerUnit = 500000;
            if (RefreshMinutes < 1) RefreshMinutes = 1;
            if (RefreshMinutes > 240) RefreshMinutes = 240;
            if (Scale < 0.6) Scale = 0.6;
            if (Scale > 2.0) Scale = 2.0;
            if (WindowOpacity < 0.3) WindowOpacity = 0.3;
            if (WindowOpacity > 1.0) WindowOpacity = 1.0;
        }

        public static string ConfigPath
        {
            get
            {
                if (_path != null) return _path;
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                if (IsWritable(dir))
                {
                    _path = Path.Combine(dir, "config.json");
                }
                else
                {
                    string alt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiangWenFengGu");
                    try { Directory.CreateDirectory(alt); }
                    catch { }
                    _path = Path.Combine(alt, "config.json");
                }
                return _path;
            }
        }

        private static bool IsWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".write_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream fs = File.Create(probe)) { }
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    AppConfig loaded = ser.Deserialize<AppConfig>(json);
                    if (loaded != null) cfg = loaded;
                }
            }
            catch { }
            cfg.Normalize();
            return cfg;
        }

        public void Save()
        {
            try
            {
                Normalize();
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(this);
                File.WriteAllText(ConfigPath, PrettyJson(json), new UTF8Encoding(false));
            }
            catch { }
        }

        private static string PrettyJson(string json)
        {
            StringBuilder sb = new StringBuilder();
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (inString)
                {
                    sb.Append(ch);
                    if (ch == '\\') { if (i + 1 < json.Length) { sb.Append(json[i + 1]); i++; } }
                    else if (ch == '"') inString = false;
                    continue;
                }
                switch (ch)
                {
                    case '"':
                        inString = true;
                        sb.Append(ch);
                        break;
                    case '{':
                    case '[':
                        sb.Append(ch);
                        indent++;
                        sb.Append('\n').Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        indent--;
                        sb.Append('\n').Append(new string(' ', indent * 2)).Append(ch);
                        break;
                    case ',':
                        sb.Append(ch).Append('\n').Append(new string(' ', indent * 2));
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
