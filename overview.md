# TempMate 项目概览

## 完成内容

从 TrafficMonitor 的温度监控能力中提取核心逻辑，创建了独立的 Windows 桌面悬浮温度工具 **TempMate**，输出到 `F:\ZB_GithubSelf\TempMate`。

## 关键决策

- **技术栈**：C# / .NET 8 / Windows Forms
- **温度库**：通过 NuGet 包 `LibreHardwareMonitorLib` 0.9.0 引入（依赖自动还原，避免直接引用 .NET Framework 版 DLL 导致的运行时 MissingMethodException）
- **UI 方案**：无边框自绘窗体；Win11 原生 Acrylic 毛玻璃 + DWM 圆角；Win10 通过 Region 裁剪圆角
- **配置持久化**：JSON 文件保存到 `%APPDATA%\TempMate\config.json`

## 已实现功能

- 桌面右下角显示 CPU / GPU / 主板 / 硬盘温度（主板取不到自动回退内存，都取不到不显示）
- 默认监控 C 盘对应硬盘温度，可在设置中切换
- 右键菜单：设置 / 总是置顶 / 鼠标穿透 / 锁定窗口位置 / 不透明度 100/75/50/25% / 退出
- 设置对话框支持显示器选择，可指定任意屏幕右下角显示
- 托盘图标，双击/右键可打开设置或退出
- 单实例运行

## 输出文件

```
F:\ZB_GithubSelf\TempMate
├── TempMate.sln
├── TempMate\          # 项目源码
├── publish\           # 自包含单文件 exe
├── build.ps1           # 一键编译脚本
├── TempMate.ico        # 应用图标
├── README.md           # 用户说明
├── TECH.md             # 技术文档
└── overview.md         # 本文件
```

## 编译运行

一键编译（推荐）：

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

输出：`publish\TempMate.exe`（单文件，需目标机器安装 .NET 8 Desktop Runtime；可配合 `TempMate.Launcher.cmd` 环境检测启动器分发）。
