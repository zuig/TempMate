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
        /// 窗口右侧到工作区右边缘的偏移（像素）。
        /// 负数表示未设置，使用绝对位置 WindowX/WindowY 回退。
        /// </summary>
        public int RightOffset { get; set; } = -1;

        /// <summary>
        /// 窗口底部到工作区底边缘的偏移（像素）。
        /// 负数表示未设置，使用绝对位置 WindowX/WindowY 回退。
        /// </summary>
        public int BottomOffset { get; set; } = -1;

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
