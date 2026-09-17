using System;
using System.IO;
using Microsoft.Win32;

namespace LiangWenFengGu
{
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "LiangWenFengGu";

        public static string ExecutablePath
        {
            get
            {
                try
                {
                    System.Reflection.Assembly asm = System.Reflection.Assembly.GetEntryAssembly();
                    if (asm != null && !string.IsNullOrEmpty(asm.Location)) return asm.Location;
                }
                catch { }
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LiangWenFengGu.exe");
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    object v = k.GetValue(ValueName);
                    return v != null && Convert.ToString(v).Length > 0;
                }
            }
            catch { return false; }
        }

        public static string CurrentValue()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return "";
                    object v = k.GetValue(ValueName);
                    return v == null ? "" : Convert.ToString(v);
                }
            }
            catch { return ""; }
        }

        public static bool Apply(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return false;
                    if (enabled)
                    {
                        string cmd = "\"" + ExecutablePath + "\"";
                        k.SetValue(ValueName, cmd, RegistryValueKind.String);
                    }
                    else
                    {
                        if (k.GetValue(ValueName) != null) k.DeleteValue(ValueName, false);
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public static void Repair()
        {
            string current = CurrentValue();
            if (current.Length == 0) return;
            string expected = "\"" + ExecutablePath + "\"";
            if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            {
                Apply(true);
            }
        }
    }
}
