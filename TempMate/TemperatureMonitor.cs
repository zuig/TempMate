using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;
using System.Management;

namespace TempMate
{
    /// <summary>
    /// 温度读取结果
    /// </summary>
    public readonly struct TemperatureReading
    {
        public float Value { get; init; }
        public bool Available { get; init; }
        public string Source { get; init; }

        public override string ToString() => Available ? $"{Value:F0}°C" : "--";
    }

    /// <summary>
    /// 硬盘信息
    /// </summary>
    public class HardDriveInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? DriveLetter { get; set; }
        public float Temperature { get; set; }
    }

    /// <summary>
    /// 基于 LibreHardwareMonitor 的温度监控核心。
    /// </summary>
    public sealed class TemperatureMonitor : IDisposable
    {
        private readonly Computer _computer;
        private readonly UpdateVisitor _visitor;
        private readonly object _lhmLock = new object();
        private bool _disposed;

        // WMI 盘符映射缓存：硬盘型号 -> 盘符。该查询较慢且很少变化，缓存 5 分钟。
        private Dictionary<string, string>? _cachedLetterMap;
        private DateTime _letterMapCachedAt = DateTime.MinValue;
        private static readonly TimeSpan LetterMapCacheTtl = TimeSpan.FromMinutes(5);

        public TemperatureMonitor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true
            };
            _visitor = new UpdateVisitor();
            _computer.Open();
        }

        /// <summary>
        /// 刷新一次硬件传感器数据。
        /// </summary>
        public void Update()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TemperatureMonitor));
            lock (_lhmLock)
            {
                _computer.Accept(_visitor);
            }
        }

        /// <summary>
        /// CPU 平均温度。
        /// </summary>
        public TemperatureReading GetCpuTemperature()
        {
            var values = new List<float>();
            string source = "CPU";

            lock (_lhmLock)
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Cpu) continue;

                    hardware.Update();
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                        {
                            values.Add(sensor.Value.Value);
                            source = sensor.Name;
                        }
                    }
                }
            }

            if (values.Count == 0)
                return new TemperatureReading { Available = false, Source = source };

            return new TemperatureReading
            {
                Value = values.Average(),
                Available = true,
                Source = source
            };
        }

        /// <summary>
        /// GPU 核心温度（NVIDIA / AMD / Intel 优先取核心温度）。
        /// </summary>
        public TemperatureReading GetGpuTemperature()
        {
            lock (_lhmLock)
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.GpuNvidia
                        && hardware.HardwareType != HardwareType.GpuAmd
                        && hardware.HardwareType != HardwareType.GpuIntel)
                        continue;

                    hardware.Update();
                    ISensor? selectedSensor = null;
                    float selectedValue = 0;

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue) continue;

                        if (selectedSensor == null)
                        {
                            selectedSensor = sensor;
                            selectedValue = sensor.Value.Value;
                        }
                        if (sensor.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
                        {
                            selectedSensor = sensor;
                            selectedValue = sensor.Value.Value;
                            break;
                        }
                    }

                    if (selectedSensor != null)
                    {
                        return new TemperatureReading
                        {
                            Value = selectedValue,
                            Available = true,
                            Source = hardware.Name
                        };
                    }
                }
            }

            return new TemperatureReading { Available = false, Source = "GPU" };
        }

        /// <summary>
        /// 主板温度。
        /// </summary>
        public TemperatureReading GetMainboardTemperature()
        {
            lock (_lhmLock)
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Motherboard) continue;

                    hardware.Update();
                    ISensor? firstTemp = null;
                    float firstValue = 0;
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue) continue;
                        if (firstTemp == null)
                        {
                            firstTemp = sensor;
                            firstValue = sensor.Value.Value;
                        }
                    }

                    // 递归检查子硬件（Super I/O 芯片通常挂在主板下）
                    if (firstTemp == null)
                    {
                        foreach (IHardware sub in hardware.SubHardware)
                        {
                            sub.Update();
                            foreach (ISensor sensor in sub.Sensors)
                            {
                                if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue) continue;
                                if (firstTemp == null)
                                {
                                    firstTemp = sensor;
                                    firstValue = sensor.Value.Value;
                                }
                            }
                        }
                    }

                    if (firstTemp != null)
                    {
                        return new TemperatureReading
                        {
                            Value = firstValue,
                            Available = true,
                            Source = hardware.Name
                        };
                    }
                }
            }

            return new TemperatureReading { Available = false, Source = "MBD" };
        }

        /// <summary>
        /// 内存温度（部分主板/内存条带有温度传感器）。
        /// 主板温度取不到时作为回退项使用。
        /// </summary>
        public TemperatureReading GetMemoryTemperature()
        {
            lock (_lhmLock)
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Memory) continue;

                    hardware.Update();
                    ISensor? firstTemp = null;
                    float firstValue = 0;
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue) continue;
                        if (firstTemp == null)
                        {
                            firstTemp = sensor;
                            firstValue = sensor.Value.Value;
                        }
                    }

                    if (firstTemp != null)
                    {
                        return new TemperatureReading
                        {
                            Value = firstValue,
                            Available = true,
                            Source = hardware.Name
                        };
                    }
                }
            }

            return new TemperatureReading { Available = false, Source = "MEM" };
        }

        /// <summary>
        /// 获取所有可识别的硬盘及其温度。
        /// </summary>
        public List<HardDriveInfo> GetAllHardDrives()
        {
            // WMI 映射较慢且与 LHM 状态无关，放在锁外执行（自身带缓存）。
            var letterMap = BuildDriveLetterMap();

            var result = new List<HardDriveInfo>();
            lock (_lhmLock)
            {
                foreach (IHardware hardware in _computer.Hardware)
                {
                    if (hardware.HardwareType != HardwareType.Storage) continue;

                    hardware.Update();
                    float? temp = null;
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                        {
                            temp = sensor.Value.Value;
                            break;
                        }
                    }

                    string key = hardware.Name;
                    letterMap.TryGetValue(key, out string? letter);

                    result.Add(new HardDriveInfo
                    {
                        Name = key,
                        DriveLetter = letter,
                        Temperature = temp ?? -1f
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 根据硬盘名称或盘符查找硬盘温度。
        /// </summary>
        public TemperatureReading GetHardDriveTemperature(string? driveLetterOrName)
        {
            var drives = GetAllHardDrives();
            if (drives.Count == 0)
                return new TemperatureReading { Available = false, Source = "HDD" };

            // 1. 按盘符匹配，例如 "C:\" 或 "C"
            if (!string.IsNullOrWhiteSpace(driveLetterOrName))
            {
                string letter = driveLetterOrName.Trim().TrimEnd(':', '\\').ToUpperInvariant();
                var byLetter = drives.FirstOrDefault(d =>
                    !string.IsNullOrEmpty(d.DriveLetter) &&
                    d.DriveLetter.TrimEnd(':', '\\').ToUpperInvariant() == letter);

                if (byLetter != null && byLetter.Temperature >= 0)
                {
                    return new TemperatureReading
                    {
                        Value = byLetter.Temperature,
                        Available = true,
                        Source = byLetter.Name
                    };
                }

                // 2. 按型号名称匹配
                var byName = drives.FirstOrDefault(d =>
                    d.Name.Equals(driveLetterOrName, StringComparison.OrdinalIgnoreCase));
                if (byName != null && byName.Temperature >= 0)
                {
                    return new TemperatureReading
                    {
                        Value = byName.Temperature,
                        Available = true,
                        Source = byName.Name
                    };
                }
            }

            // 3. 兜底：返回第一个有温度的硬盘
            var first = drives.FirstOrDefault(d => d.Temperature >= 0);
            if (first != null)
            {
                return new TemperatureReading
                {
                    Value = first.Temperature,
                    Available = true,
                    Source = first.Name
                };
            }

            return new TemperatureReading { Available = false, Source = "HDD" };
        }

        /// <summary>
        /// 通过 WMI 建立 硬盘型号 -> 盘符 映射（带 5 分钟缓存）。
        /// </summary>
        private Dictionary<string, string> BuildDriveLetterMap()
        {
            if (_cachedLetterMap != null && DateTime.Now - _letterMapCachedAt < LetterMapCacheTtl)
                return _cachedLetterMap;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"SELECT * FROM Win32_DiskDrive");

                foreach (ManagementObject disk in searcher.Get().Cast<ManagementObject>())
                {
                    string model = (disk["Model"]?.ToString() ?? string.Empty).Trim();
                    string letters = GetDriveLettersForDisk(disk);
                    if (!string.IsNullOrEmpty(model) && !string.IsNullOrEmpty(letters))
                    {
                        map[model] = letters;
                    }
                }
            }
            catch
            {
                // WMI 失败时继续，只是不能显示盘符
            }

            _cachedLetterMap = map;
            _letterMapCachedAt = DateTime.Now;
            return map;
        }

        private static string GetDriveLettersForDisk(ManagementObject disk)
        {
            var letters = new List<string>();
            try
            {
                string deviceId = disk["DeviceID"]?.ToString() ?? string.Empty;
                string query = $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{deviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";
                using var partSearcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject partition in partSearcher.Get().Cast<ManagementObject>())
                {
                    string partDeviceId = partition["DeviceID"]?.ToString() ?? string.Empty;
                    string logicalQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partDeviceId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";
                    using var logicalSearcher = new ManagementObjectSearcher(logicalQuery);
                    foreach (ManagementObject logical in logicalSearcher.Get().Cast<ManagementObject>())
                    {
                        string? letter = logical["DeviceID"]?.ToString();
                        if (!string.IsNullOrEmpty(letter))
                            letters.Add(letter);
                    }
                }
            }
            catch
            {
                // 忽略 WMI 错误
            }

            return string.Join(", ", letters);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _computer.Close();
        }

        /// <summary>
        /// LibreHardwareMonitor 需要的访问器。
        /// </summary>
        private class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) => computer.Traverse(this);

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware subHardware in hardware.SubHardware)
                    subHardware.Accept(this);
            }

            public void VisitSensor(ISensor sensor) { }

            public void VisitParameter(IParameter parameter) { }
        }
    }
}
