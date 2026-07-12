# TempMate

一个从 [TrafficMonitor](https://github.com/zhongyang219/TrafficMonitor) 温度监控思路中提取、基于 [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 的 Windows 桌面悬浮温度小工具。

## 功能

- 桌面右下角悬浮显示四个核心温度：
  - **CPU 温度**
  - **GPU 温度**
  - **主板温度**
  - **硬盘温度**（默认 C 盘对应硬盘，可在设置中切换）
- 右键菜单快速开关：
  - 总是置顶
  - 鼠标穿透
  - 锁定窗口位置
  - 窗口不透明度（100% / 75% / 50% / 25%）
- 多显示器支持：可在设置中指定任意一块屏幕的右下角显示
- 单实例运行，配置自动保存到 `%APPDATA%\TempMate\config.json`

## 截图示意

窗口样式参考 TrafficMonitor 的半透明悬浮窗：浅绿色背景、黑色文字，四行分别显示 CPU / GPU / 主板 / 硬盘温度。

## 运行环境

- Windows 10 / Windows 11
- .NET 8.0 运行时（已安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) 的系统可直接运行 `TempMate.exe`）
- 若双击无反应，请查看程序所在目录下的 `crash.log` 获取错误栈

## 快速开始

### 编译

仓库提供 `build.ps1` 一键编译（框架依赖、单文件发布）：

```powershell
.\build.ps1
# 产物目录 publish\
#   TempMate.exe          主程序（需 .NET 8 Desktop Runtime）
#   TempMate.Launcher.cmd 环境检测启动器
```

如需手动编译：

```powershell
dotnet publish TempMate\TempMate.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

### 运行

- 已安装 .NET 8 Desktop Runtime：直接双击 `TempMate.exe`，或通过 `TempMate.Launcher.cmd` 启动。
- 未安装运行时：双击 `TempMate.Launcher.cmd`，按提示跳转官网下载安装后重试。

### 分发给他人

将 `publish\` 目录下的 `TempMate.exe` 与 `TempMate.Launcher.cmd` 一并打包即可。对方双击启动器：已安装运行时则直接运行，未安装则提示跳转官方下载页。

### （可选）自包含发布

若目标机器完全没有 .NET 运行时、且不方便安装，可打成自包含单文件（体积约 100 MB 以上）：

```powershell
dotnet publish TempMate\TempMate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-self
```

发布完成后，`publish-self\TempMate.exe` 即为不依赖运行时的单文件可执行程序。

### 启动器说明

框架依赖版 `TempMate.exe` 在目标电脑**缺少 .NET 8 Desktop Runtime** 时无法自行提示（CLR 加载失败，托管代码不会执行）。`TempMate.Launcher.cmd` 在拉起程序前先做环境自检：

- 检测 `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0*` 是否存在；
- 已安装 → 直接运行 `TempMate.exe`；
- 未安装 → 弹出提示框，点击 **Yes** 跳转官方下载页 <https://dotnet.microsoft.com/download/dotnet/8.0>。

> 启动器使用纯 ASCII 批处理，避免中文编码在不同编辑器 / 系统代码页下乱码；提示语为英文，不影响功能。

## 项目结构

```
TempMate/
├── .gitignore
├── TempMate.sln
├── TempMate.ico
├── TempMate.Launcher.cmd   # 环境检测启动器（分发用）
├── build.ps1               # 一键编译脚本
├── README.md
├── TECH.md
├── overview.md
└── TempMate/
    ├── TempMate.csproj      # 项目文件（.NET 8 Windows Forms）
    ├── Program.cs           # 程序入口，单实例互斥体 + 全局异常日志
    ├── AppConfig.cs         # 配置读写（JSON）
    ├── TemperatureMonitor.cs# 温度监控核心（LibreHardwareMonitor 封装）
    ├── MainForm.cs          # 悬浮窗主界面
    ├── SettingsForm.cs      # 设置对话框
    ├── StartupHelper.cs     # 开机自启动注册表管理
    └── app.manifest         # DPI 感知与管理员相关声明
```

> 依赖通过 NuGet 包 `LibreHardwareMonitorLib` 0.9.0 引入（详见 `TECH.md` 第 4 节），
> 不再需要手动放置 `LibreHardwareMonitorLib.dll`。

## 与 TrafficMonitor 的关系

TempMate 提取了 TrafficMonitor 中“通过 LibreHardwareMonitor 读取硬件温度”的核心能力，但没有复用其完整的网络/任务栏/皮肤系统。具体差异见 `TECH.md`。

## 许可证

本项目代码部分以 MIT 许可证发布。`LibreHardwareMonitorLib.dll` 来自 [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)，遵循其原有许可证（MPL-2.0）。
