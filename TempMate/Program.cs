using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace TempMate
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            // 未处理异常统一写日志，避免"启动后什么都没有"却无从排查
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                WriteCrashLog("UnhandledException", e.ExceptionObject as Exception);
            Application.ThreadException += (s, e) =>
                WriteCrashLog("ThreadException", e.Exception);

            // 单实例运行
            const string mutexName = "TempMate_SingleInstance_Mutex";
            bool createdNew;
            _mutex = new Mutex(true, mutexName, out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("TempMate 已经在运行。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                WriteCrashLog("Run", ex);
                MessageBox.Show("TempMate 启动失败：\n" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _mutex.ReleaseMutex();
        }

        private static void WriteCrashLog(string tag, Exception? ex)
        {
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {tag}:{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { /* 忽略日志失败 */ }
        }
    }
}
