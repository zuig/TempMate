using Microsoft.Win32;
using System.Windows.Forms;

namespace TempMate
{
    /// <summary>
    /// 管理"开机自启动"：读写 HKCU\Software\Microsoft\Windows\CurrentVersion\Run。
    /// </summary>
    public static class StartupHelper
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TempMate";

        /// <summary>
        /// 当前是否已设置开机自启动。
        /// </summary>
        public static bool IsStartupEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(AppName) is not null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置或取消开机自启动。
        /// </summary>
        public static void SetStartup(bool enabled)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                    ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

                if (enabled)
                {
                    string exePath = Application.ExecutablePath;
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue(AppName, throwOnMissingValue: false);
                }
            }
            catch
            {
                // 写入失败时静默忽略，避免崩溃
            }
        }
    }
}
