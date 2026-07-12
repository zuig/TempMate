# TempMate 技术文档

## 1. 实现思路

TrafficMonitor 的温度监控能力来自它封装的 `OpenHardwareMonitorApi` 项目，本质上是一个 C++/CLI 桥接层，调用 .NET 库 `LibreHardwareMonitorLib.dll` 读取传感器数据。

TempMate 做了更直接的提取：

1. **直接使用 LibreHardwareMonitorLib**：用 C# 项目引用该 DLL，无需再写 C++/CLI 桥接。
2. **只保留温度相关功能**：去掉了网络、CPU/内存占用、任务栏嵌入、皮肤等模块。
3. **独立悬浮窗**：用 Windows Forms 无边框窗体 + GDI+ 自绘，实现和 TrafficMonitor 主悬浮窗类似的视觉效果。

## 2. 核心类说明

### `TemperatureMonitor`

封装 LibreHardwareMonitor 的 `Computer` 对象，负责：

- 初始化时启用 CPU / GPU / 主板 / 硬盘传感器
- 每秒调用 `Update()` 刷新硬件树
- 提供四个读取方法：
  - `GetCpuTemperature()`：取 CPU 所有温度传感器的平均值
  - `GetGpuTemperature()`：取 NVIDIA/AMD/Intel GPU 的 "GPU Core" 温度，找不到则取第一个温度传感器
  - `GetMainboardTemperature()`：取主板或子硬件（Super I/O）的第一个温度传感器
  - `GetHardDriveTemperature(string driveLetterOrName)`：按盘符或硬盘名称匹配温度

### 硬盘与盘符映射

LibreHardwareMonitor 返回的 Storage 硬件只有型号名称（如 `Samsung SSD 980 PRO 1TB`），没有直接给出盘符。TempMate 通过 WMI 查询：

```
Win32_DiskDrive
  → Win32_DiskDriveToDiskPartition
    → Win32_LogicalDiskToPartition
      → Win32_LogicalDisk
```

建立“硬盘型号 → 盘符”映射，从而让用户按 `C:`、`D:` 等盘符选择要监控的硬盘。

### `MainForm`

无边框置顶窗体：

- `FormBorderStyle = None`
- `TopMost = true`
- `Opacity` 根据配置在 1.0 / 0.75 / 0.50 / 0.25 之间切换
- 鼠标穿透通过 Win32 API `SetWindowLong` 设置 `WS_EX_TRANSPARENT` 实现
- 未锁定时可拖拽；位置变化后自动保存
- 右键菜单提供设置入口和常用开关

### `AppConfig`

配置保存在：

```
%APPDATA%\TempMate\config.json
```

包含：

| 字段 | 说明 |
|------|------|
| `TopMost` | 是否置顶 |
| `MousePassThrough` | 鼠标是否穿透 |
| `LockPosition` | 是否禁止拖拽 |
| `OpacityPercent` | 不透明度百分比 |
| `DriveLetter` | 监控的硬盘盘符 |
| `UseSecondaryScreen` | 是否在副屏显示 |
| `WindowX` / `WindowY` | 窗口位置 |

## 3. 构建说明

### 环境要求

- Windows 10/11
- [.NET 5 SDK](https://dotnet.microsoft.com/download/dotnet/5.0)（本机只有 .NET 5 SDK，因此项目目标框架选用了 `net5.0-windows`）

### 编译

```powershell
dotnet build TempMate.sln -c Release
```

### 自包含发布

如果目标电脑没有安装 .NET 5 Runtime：

```powershell
dotnet publish TempMate\TempMate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

## 4. 依赖与版本选择（踩坑记录）

`LibreHardwareMonitorLib` 在调用 `Computer.Open()` 加载内核驱动时，会依赖一系列 .NET 程序集：
`System.IO.FileSystem.AccessControl`、`System.Security.AccessControl`、
`System.Security.Principal.Windows`、`System.Management`、`System.IO.Ports`、
`HidSharp`、`Microsoft.Win32.Registry` 等。

**最初的错误做法**：直接引用 TrafficMonitor 仓库里自带的 `LibreHardwareMonitorLib.dll`
（该 DLL 面向 .NET Framework 编译），仅通过 NuGet 补了 `System.Management`。结果运行时
抛出 `System.MissingMethodException: Method not found:
'System.Security.AccessControl.FileSecurity System.IO.FileInfo.GetAccessControl()'`，
程序在 `Computer.Open()` 处直接崩溃，表现为"运行后什么都没有"。

**最终正确做法**：改用 NuGet 上的 `LibreHardwareMonitorLib` 0.9.0。它的全部依赖会被
NuGet 自动还原为适配 .NET 5 的版本（System.Management 6.0.0 等），运行时不再缺方法，
`Open()` 成功。0.9.3+ 版本要求 `System.Management >= 8.0.0`，与 .NET 5 不兼容，因此锁定 0.9.0。

> 结论：`LibreHardwareMonitorLib` 必须作为 **NuGet 包**引入（依赖自动还原），
> 不要直接拷贝一个针对 .NET Framework 编译的 DLL 到 .NET Core/5 项目里。

## 5. 扩展建议

- **任务栏/托盘图标**：可在 `MainForm` 中加入 `NotifyIcon`，实现开机自启和最小化到托盘。
- **更多温度项**：`TemperatureMonitor` 已暴露 `GetAllHardDrives()`，可扩展为多硬盘列表或显示 CPU 各核心温度。
- **皮肤/颜色配置**：当前颜色硬编码在 `MainForm.BackColor` 和 `DrawRow` 中，可抽出到 `AppConfig` 实现自定义。
- **高温告警**：在 `UpdateTimer_Tick` 中判断温度阈值并弹出提示。
- **开机启动**：在注册表 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 写入程序路径。

## 6. 参考资料

- [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor)
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- [LibreHardwareMonitorLib NuGet](https://www.nuget.org/packages/LibreHardwareMonitorLib)
