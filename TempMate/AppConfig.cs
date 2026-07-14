using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TempMate
{
    /// <summary>
    /// 应用配置，自动持久化到用户目录下的 JSON 文件。
    /// </summary>
    public class AppConfig
    {
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TempMate",
            "config.json");

        public bool TopMost { get; set; } = true;
        public bool MousePassThrough { get; set; } = false;
        public bool LockPosition { get; set; } = false;
        public int OpacityPercent { get; set; } = 100;
        public string DriveLetter { get; set; } = "";
        public int WindowX { get; set; } = int.MinValue;
        public int WindowY { get; set; } = int.MinValue;

        /// <summary>
        /// 记录的窗口右边缘绝对屏幕坐标（像素）。
        /// int.MinValue 表示尚无记录，此时才需要计算一次默认位置。
        /// </summary>
        public int AnchorRight { get; set; } = int.MinValue;

        /// <summary>
        /// 记录的窗口下边缘绝对屏幕坐标（像素）。
        /// int.MinValue 表示尚无记录，此时才需要计算一次默认位置。
        /// </summary>
        public int AnchorBottom { get; set; } = int.MinValue;

        public bool StartWithWindows { get; set; } = false;

        /// <summary>
        /// 未显式指定硬盘时，返回 Windows 系统所在盘符（如 "C"）。
        /// </summary>
        public static string SystemDriveLetter()
        {
            string? root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrEmpty(root)) return "C";
            return root.TrimEnd('\\', ':').ToUpperInvariant();
        }

        [JsonIgnore]
        public double WindowOpacity => OpacityPercent switch
        {
            75 => 0.75,
            50 => 0.50,
            25 => 0.25,
            _ => 1.00,
        };

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }

            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
