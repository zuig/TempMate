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
- 支持双屏：可选择在副屏右下角显示（设置中开启）
- 单实例运行，配置自动保存到 `%APPDATA%\TempMate\config.json`

## 截图示意

窗口样式参考 TrafficMonitor 的半透明悬浮窗：浅绿色背景、黑色文字，四行分别显示 CPU / GPU / 主板 / 硬盘温度。

## 运行环境

- Windows 10 / Windows 11
- .NET 5.0 运行时（已安装 [.NET 5 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/5.0) 的系统可直接运行 `TempMate.exe`）
- 若双击无反应，请查看程序所在目录下的 `crash.log` 获取错误栈

## 快速开始

### 方式一：直接运行（需要本机已安装 .NET 5 Runtime）

```powershell
cd TempMate\bin\Release\net5.0-windows
.\TempMate.exe
```

### 方式二：自包含发布（无需安装运行库）

```powershell
cd F:\ZB_GithubSelf\TempMate
dotnet publish TempMate\TempMate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

发布完成后，`publish\TempMate.exe` 即为单文件可执行程序。

### 分发给他人的推荐方式：启动器（环境自检）

框架依赖版 `TempMate.exe` 在目标电脑**缺少 .NET 5 Desktop Runtime** 时无法自行提示（CLR 加载失败，托管代码不会执行）。为此提供了 `TempMate.Launcher.cmd`：

- 启动前先检测 `C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\5.0*` 是否存在；
- 已安装 → 直接拉起 `TempMate.exe`；
- 未安装 → 弹出提示框（英文），点击 **Yes** 即跳转官方下载页 <https://dotnet.microsoft.com/download/dotnet/5.0>。

> 注意：启动器使用纯 ASCII 批处理，避免中文编码在不同编辑器/系统代码页下乱码。提示语为英文，不影响功能。

**所以把 TempMate 发给别人时，请连同 `TempMate.Launcher.cmd` 一起给，并让对方双击启动器即可。** 你自己本机已装 .NET 5，直接双击 `TempMate.exe` 也没问题。

## 项目结构

```
TempMate/
├── TempMate.sln
├── TempMate/
│   ├── TempMate.csproj      # 项目文件（.NET 5 Windows Forms）
│   ├── Program.cs           # 程序入口，单实例互斥体 + 全局异常日志
│   ├── AppConfig.cs         # 配置读写（JSON）
│   ├── TemperatureMonitor.cs# 温度监控核心（LibreHardwareMonitor 封装）
│   ├── MainForm.cs          # 悬浮窗主界面
│   ├── SettingsForm.cs      # 设置对话框
│   └── app.manifest         # DPI 感知与管理员相关声明
├── README.md
├── TECH.md
└── overview.md
```

> 依赖通过 NuGet 包 `LibreHardwareMonitorLib` 0.9.0 引入（详见 `TECH.md` 第 4 节），
> 不再需要手动放置 `LibreHardwareMonitorLib.dll`。

## 与 TrafficMonitor 的关系

TempMate 提取了 TrafficMonitor 中“通过 LibreHardwareMonitor 读取硬件温度”的核心能力，但没有复用其完整的网络/任务栏/皮肤系统。具体差异见 `TECH.md`。

## 许可证

本项目代码部分以 MIT 许可证发布。`LibreHardwareMonitorLib.dll` 来自 [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)，遵循其原有许可证（MPL-2.0）。
