using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace LiangWenFengGu
{
    public class BalanceResult
    {
        public bool Ok;
        public string Error;
        public double Remaining;
        public double Used;
        public double Total;
        public string PlanName;
        public DateTime FetchedAt;
        public string RawEndpoint;
        public string Symbol;
        public string Extra;
        public bool Official;

        public BalanceResult()
        {
            Error = "";
            PlanName = "";
            Symbol = "\u00A5";
            Extra = "";
            FetchedAt = ClockService.Now();
        }
    }

    public static class BalanceClient
    {
        public const string ModeNewApi = "newapi";
        public const string ModeOfficial = "official";

        public static bool IsOfficial(string mode)
        {
            return string.Equals(mode, ModeOfficial, StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeMode(string mode)
        {
            return IsOfficial(mode) ? ModeOfficial : ModeNewApi;
        }

        public static string NormalizeBaseUrl(string raw)
        {
            string url = (raw == null) ? "" : raw.Trim();
            if (url.Length == 0) return "";
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            url = url.TrimEnd('/');
            string[] suffixes = new string[] { "/v1", "/api", "/v1/chat/completions", "/chat/completions" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (url.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    url = url.Substring(0, url.Length - suffixes[i].Length).TrimEnd('/');
                }
            }
            return url;
        }

        public static BalanceResult Query(AppConfig cfg)
        {
            return IsOfficial(cfg.BalanceMode) ? QueryOfficial(cfg) : QueryNewApi(cfg);
        }

        private static BalanceResult QueryNewApi(AppConfig cfg)
        {
            BalanceResult res = new BalanceResult();
            res.Symbol = cfg.CurrencySymbol;
            string baseUrl = NormalizeBaseUrl(cfg.BaseUrl);
            if (baseUrl.Length == 0)
            {
                res.Error = "\u672A\u914D\u7F6E\u8BF7\u6C42\u5730\u5740";
                return res;
            }
            if (string.IsNullOrEmpty(cfg.AccessToken))
            {
                res.Error = "\u672A\u914D\u7F6E\u8BBF\u95EE\u4EE4\u724C";
                return res;
            }
            string url = baseUrl + "/api/user/self";
            res.RawEndpoint = url;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                req.UserAgent = "LiangWenFengGu/1.0";
                req.Accept = "application/json";
                req.Headers["Authorization"] = "Bearer " + cfg.AccessToken;
                if (!string.IsNullOrEmpty(cfg.UserId))
                {
                    req.Headers["New-Api-User"] = cfg.UserId;
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string body = ReadBody(resp);
                    Parse(res, body, cfg);
                }
            }
            catch (WebException we)
            {
                string body = "";
                if (we.Response != null)
                {
                    try { body = ReadBody((HttpWebResponse)we.Response); }
                    catch { }
                }
                string message = ExtractMessage(body);
                if (message.Length > 0) res.Error = message;
                else if (we.Status == WebExceptionStatus.Timeout) res.Error = "\u8BF7\u6C42\u8D85\u65F6";
                else res.Error = "\u8FDE\u63A5\u5931\u8D25\uFF08" + we.Message + "\uFF09";
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
            }
            return res;
        }

        private static BalanceResult QueryOfficial(AppConfig cfg)
        {
            BalanceResult res = new BalanceResult();
            res.Official = true;
            res.Symbol = cfg.CurrencySymbol;
            string baseUrl = NormalizeBaseUrl(cfg.OfficialBaseUrl);
            if (baseUrl.Length == 0)
            {
                res.Error = "\u672A\u914D\u7F6E\u5B98\u65B9\u5730\u5740";
                return res;
            }
            if (string.IsNullOrEmpty(cfg.OfficialApiKey))
            {
                res.Error = "\u672A\u914D\u7F6E\u5B98\u65B9 API Key";
                return res;
            }
            string host = "";
            try { host = new Uri(baseUrl).Host.ToLowerInvariant(); }
            catch { }

            // \u4E0E cc-switch \u4E00\u81F4\uFF1A\u6309\u5B98\u65B9\u7AD9\u70B9\u5339\u914D\u4F59\u989D\u63A5\u53E3
            string path = "/user/balance";
            string kind = "deepseek";
            if (host.EndsWith("api.siliconflow.cn") || host.EndsWith("api.siliconflow.com"))
            {
                path = "/v1/user/info";
                kind = "siliconflow";
            }
            else if (host.EndsWith("openrouter.ai"))
            {
                path = "/api/v1/credits";
                kind = "openrouter";
            }
            else if (host.EndsWith("api.novita.ai"))
            {
                path = "/v3/user/balance";
                kind = "novita";
            }

            string url = baseUrl + path;
            res.RawEndpoint = url;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch { }

            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                req.UserAgent = "LiangWenFengGu/1.0";
                req.Accept = "application/json";
                req.Headers["Authorization"] = "Bearer " + cfg.OfficialApiKey;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    ParseOfficial(res, ReadBody(resp), kind, host);
                }
            }
            catch (WebException we)
            {
                string body = "";
                if (we.Response != null)
                {
                    try { body = ReadBody((HttpWebResponse)we.Response); }
                    catch { }
                }
                string message = ExtractMessage(body);
                if (message.Length > 0) res.Error = message;
                else if (we.Status == WebExceptionStatus.Timeout) res.Error = "\u8BF7\u6C42\u8D85\u65F6";
                else res.Error = "\u8FDE\u63A5\u5931\u8D25\uFF08" + we.Message + "\uFF09";
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
            }
            return res;
        }

        private static void ParseOfficial(BalanceResult res, string body, string kind, string host)
        {
            if (string.IsNullOrEmpty(body))
            {
                res.Error = "\u54CD\u5E94\u4E3A\u7A7A";
                return;
            }
            object root;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = 8 * 1024 * 1024;
                root = ser.DeserializeObject(body);
            }
            catch (Exception ex)
            {
                res.Error = "\u54CD\u5E94\u89E3\u6790\u5931\u8D25\uFF1A" + ex.Message;
                return;
            }
            System.Collections.Generic.Dictionary<string, object> d = root as System.Collections.Generic.Dictionary<string, object>;
            if (d == null)
            {
                res.Error = "\u54CD\u5E94\u683C\u5F0F\u5F02\u5E38";
                return;
            }

            if (kind == "siliconflow")
            {
                System.Collections.Generic.Dictionary<string, object> data = Child(d, "data");
                if (data == null) data = d;
                res.Symbol = "\u00A5";
                res.Remaining = ToDouble(data, "totalBalance");
                res.PlanName = "SiliconFlow \u5B98\u65B9";
                res.Extra = "\u5145\u503C\u4F59\u989D\uFF1A" + res.Symbol + ToDouble(data, "chargeBalance").ToString("N2", CultureInfo.InvariantCulture)
                    + "\n\u8D60\u9001\u4F59\u989D\uFF1A" + res.Symbol + ToDouble(data, "balance").ToString("N2", CultureInfo.InvariantCulture);
                res.Ok = true;
                return;
            }

            if (kind == "openrouter")
            {
                System.Collections.Generic.Dictionary<string, object> data = Child(d, "data");
                if (data == null) data = d;
                double credits = ToDouble(data, "total_credits");
                double usage = ToDouble(data, "total_usage");
                res.Symbol = "$";
                res.Remaining = credits - usage;
                res.Used = usage;
                res.Total = credits;
                res.PlanName = "OpenRouter \u5B98\u65B9";
                res.Ok = true;
                return;
            }

            if (kind == "novita")
            {
                res.Symbol = "$";
                res.Remaining = ToDouble(d, "availableBalanceUSD");
                res.PlanName = "Novita \u5B98\u65B9";
                res.Ok = true;
                return;
            }

            object[] infos = ChildArray(d, "balance_infos");
            System.Collections.Generic.Dictionary<string, object> info = null;
            if (infos != null && infos.Length > 0) info = infos[0] as System.Collections.Generic.Dictionary<string, object>;
            if (info == null)
            {
                if (!d.ContainsKey("total_balance"))
                {
                    res.Error = ExtractMessage(body);
                    if (res.Error.Length == 0) res.Error = "\u67E5\u8BE2\u5931\u8D25";
                    return;
                }
                info = d;
            }
            res.Symbol = SymbolFor(Convert.ToString(info.ContainsKey("currency") ? info["currency"] : ""));
            res.Remaining = ToDouble(info, "total_balance");
            res.PlanName = host.IndexOf("deepseek", StringComparison.OrdinalIgnoreCase) >= 0 ? "DeepSeek \u5B98\u65B9" : host;
            res.Extra = "\u5145\u503C\u4F59\u989D\uFF1A" + res.Symbol + ToDouble(info, "topped_up_balance").ToString("N2", CultureInfo.InvariantCulture)
                + "\n\u8D60\u9001\u4F59\u989D\uFF1A" + res.Symbol + ToDouble(info, "granted_balance").ToString("N2", CultureInfo.InvariantCulture);
            if (d.ContainsKey("is_available") && d["is_available"] != null)
            {
                string available = Convert.ToString(d["is_available"]);
                if (string.Equals(available, "false", StringComparison.OrdinalIgnoreCase)) res.Extra += "\n\uFF08\u8BE5\u8D26\u53F7\u4F59\u989D\u4E0D\u53EF\u7528\uFF09";
            }
            res.Ok = true;
        }

        private static System.Collections.Generic.Dictionary<string, object> Child(System.Collections.Generic.Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key)) return null;
            return d[key] as System.Collections.Generic.Dictionary<string, object>;
        }

        private static object[] ChildArray(System.Collections.Generic.Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key)) return null;
            return d[key] as object[];
        }

        private static string SymbolFor(string currency)
        {
            if (string.IsNullOrEmpty(currency)) return "\u00A5";
            string c = currency.Trim().ToUpperInvariant();
            if (c == "CNY" || c == "RMB") return "\u00A5";
            if (c == "USD") return "$";
            return currency.Trim();
        }

        private static string ReadBody(HttpWebResponse resp)
        {
            using (Stream s = resp.GetResponseStream())
            {
                if (s == null) return "";
                using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private static string ExtractMessage(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                object o = ser.DeserializeObject(body);
                System.Collections.Generic.Dictionary<string, object> d = o as System.Collections.Generic.Dictionary<string, object>;
                if (d != null)
                {
                    if (d.ContainsKey("message") && d["message"] != null) return Convert.ToString(d["message"]);
                    if (d.ContainsKey("error") && d["error"] != null)
                    {
                        System.Collections.Generic.Dictionary<string, object> e = d["error"] as System.Collections.Generic.Dictionary<string, object>;
                        if (e != null && e.ContainsKey("message")) return Convert.ToString(e["message"]);
                        return Convert.ToString(d["error"]);
                    }
                }
            }
            catch { }
            string trimmed = body.Trim();
            if (trimmed.Length > 160) trimmed = trimmed.Substring(0, 160);
            return trimmed;
        }

        private static void Parse(BalanceResult res, string body, AppConfig cfg)
        {
            if (string.IsNullOrEmpty(body))
            {
                res.Error = "\u54CD\u5E94\u4E3A\u7A7A";
                return;
            }
            object root;
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = 8 * 1024 * 1024;
                root = ser.DeserializeObject(body);
            }
            catch (Exception ex)
            {
                res.Error = "\u54CD\u5E94\u89E3\u6790\u5931\u8D25\uFF1A" + ex.Message;
                return;
            }
            System.Collections.Generic.Dictionary<string, object> d = root as System.Collections.Generic.Dictionary<string, object>;
            if (d == null)
            {
                res.Error = "\u54CD\u5E94\u683C\u5F0F\u5F02\u5E38";
                return;
            }
            bool success = true;
            if (d.ContainsKey("success") && d["success"] != null)
            {
                try { success = Convert.ToBoolean(d["success"]); }
                catch { success = true; }
            }
            System.Collections.Generic.Dictionary<string, object> data = null;
            if (d.ContainsKey("data")) data = d["data"] as System.Collections.Generic.Dictionary<string, object>;
            if (!success || data == null)
            {
                res.Error = ExtractMessage(body);
                if (res.Error.Length == 0) res.Error = "\u67E5\u8BE2\u5931\u8D25";
                return;
            }
            double divisor = cfg.QuotaPerUnit > 0 ? cfg.QuotaPerUnit : 500000.0;
            double quota = ToDouble(data, "quota");
            double usedQuota = ToDouble(data, "used_quota");
            res.Remaining = quota / divisor;
            res.Used = usedQuota / divisor;
            res.Total = (quota + usedQuota) / divisor;
            if (data.ContainsKey("group") && data["group"] != null) res.PlanName = Convert.ToString(data["group"]);
            if (res.PlanName.Length == 0 && data.ContainsKey("username") && data["username"] != null) res.PlanName = Convert.ToString(data["username"]);
            res.Ok = true;
        }

        private static double ToDouble(System.Collections.Generic.Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return 0;
            object v = d[key];
            if (v is double) return (double)v;
            if (v is decimal) return (double)(decimal)v;
            if (v is int) return (double)(int)v;
            if (v is long) return (double)(long)v;
            double parsed;
            if (double.TryParse(Convert.ToString(v), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)) return parsed;
            return 0;
        }
    }
}
