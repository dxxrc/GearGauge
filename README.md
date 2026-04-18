# GearGauge

[English](README.en.md) | 简体中文

> 说明
>
> 本项目的软件需求整理、功能设计、代码实现与文档撰写主要由 AI 辅助完成。本人并非相关专业开发者，对底层实现、构建细节和许可证条款的理解仍然有限；如果文档、实现或合规说明存在错误，欢迎通过 Issue 或 Pull Request 指正，感谢赐教。

## 项目简介

GearGauge 是一个面向 Windows 10 / Windows 11 的实时硬件与帧率监控工具，当前提供两种使用方式：

- `GearGauge.UI`：WPF 图形界面，包含主面板、系统托盘、桌面悬浮窗、任务栏小组件
- `GearGauge.Cli`：命令行采样工具，可输出 JSON 快照，便于脚本或日志采集

项目目标不是做“全能跑分软件”，而是把日常最常用的运行态指标整合到一个轻量工具里，方便观察电脑当前是否处于高负载、游戏是否正常输出帧率、网络是否正在持续传输，以及 CPU / GPU / 内存是否存在明显异常。

## 主要功能

- 实时显示 CPU 使用率、温度、功耗、频率与分核心信息
- 实时显示 GPU 使用率、温度、功耗、频率与显卡类型
- 显示内存总量、已用、可用与占用率
- 显示显示输出 FPS、游戏 FPS、当前活跃内容
- 显示网卡上下行速率，并自动选择当前活跃网卡
- 提供主界面、系统托盘、桌面悬浮窗、任务栏小组件
- 支持中英文本地化、主题、采样间隔、小数位、温度单位、自动启动等设置
- CLI 支持单次采样、持续采样、漂亮格式 JSON、传感器转储

## 工作原理

GearGauge 不是依赖单一数据源，而是按指标类型组合多个 Windows 能力与第三方库：

- 硬件传感器
  - 主要通过 `LibreHardwareMonitorLib` 读取 CPU / GPU 传感器
  - CPU 频率会结合 `PDH` 读取结果补全
  - CPU / 内存型号、核心数等元数据通过 `WMI` 获取
  - 某些温度 / 功耗缺失时，支持从 `HWiNFO` 共享内存或 `WMI` 回退补全
- 帧率采集
  - 显示输出 FPS 优先走 `DXGI` 桌面复制计数
  - 游戏 FPS 优先通过 `ETW` 抓取 DXGI / D3D9 / DxgKrnl 的 present 事件
  - 当进程级 FPS 不稳定时，会结合 `DWM` 窗口帧时序作为回退参考
- 网络速率
  - 通过 `System.Net.NetworkInformation` 读取网卡累计字节数
  - 用两次采样之间的差值计算上传 / 下载 Mbps
- UI 与系统集成
  - 主界面基于 `WPF`
  - 系统托盘通过 `Hardcodet.NotifyIcon.Wpf`
  - 开机自启动通过 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

## 输出指标

当前项目会输出或显示以下核心指标：

- `CPU`：型号、总使用率、温度、功耗、平均频率、分核心指标
- `GPU`：名称、类型、使用率、温度、功耗、频率
- `Memory`：总量、已用、可用、占用率
- `FPS`：显示输出 FPS、游戏 FPS、目标显示器、当前活跃内容、数据源状态
- `NetworkAdapters`：网卡名称、类型、连接状态、上下行速率、是否被选为当前主网卡
- `ActiveMonitors`：活动显示器元数据

## 系统要求

- Windows 10 或 Windows 11 x64
- .NET SDK 8.0（仅构建 / 测试 / 发布时需要）
- 首次 `dotnet restore` 需要联网
- 如果需要更完整的 ETW 游戏 FPS 采集，建议以管理员权限运行

## 构建与运行

为了避免当前工作区的首次运行问题，建议先设置本地 `.dotnet` 目录：

```powershell
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_HOME="$PWD\.dotnet"
```

恢复依赖：

```powershell
dotnet restore GearGauge.sln
```

构建：

```powershell
dotnet build GearGauge.sln -c Release --no-restore -m:1
```

测试：

```powershell
dotnet test GearGauge.Tests\GearGauge.Tests.csproj -c Release --no-build -m:1
```

运行图形界面：

```powershell
dotnet run --project GearGauge.UI\GearGauge.UI.csproj -c Release --no-build
```

运行 CLI，输出单次快照：

```powershell
dotnet run --project GearGauge.Cli\GearGauge.Cli.csproj -c Release --no-build -- --pretty
```

持续采样：

```powershell
dotnet run --project GearGauge.Cli\GearGauge.Cli.csproj -c Release --no-build -- --watch --interval-ms 1000 --iterations 10 --pretty
```

导出底层传感器列表：

```powershell
dotnet run --project GearGauge.Cli\GearGauge.Cli.csproj -c Release --no-build -- --dump-sensors
```

发布 CLI：

```powershell
dotnet publish GearGauge.Cli\GearGauge.Cli.csproj -c Release -r win-x64 --self-contained false -m:1
```

发布 UI：

```powershell
dotnet publish GearGauge.UI\GearGauge.UI.csproj -c Release -r win-x64 --self-contained false -m:1
```

## 使用说明与已知限制

- 网络速率的第一次采样通常会是 `0`，因为它需要至少两次快照计算差值
- 游戏 FPS 依赖 ETW 与系统图形事件；权限不足时会在 `ProviderStatus` 中说明原因
- 某些温度、功耗、频率指标是否可见，取决于硬件、驱动和底层传感器暴露情况
- `VideoPlaybackFps` 当前模型中预留但未作为稳定指标对外承诺
- 任务栏小组件和悬浮窗更适合日常状态观察，不等同于专业压测或基准测试工具

## GitHub 文档组织

GitHub 不会根据访问者语言自动切换仓库首页 README。平台只会按固定位置优先级展示一个 README 文件；因此本仓库采用下面的结构：

- `README.md`：中文主文档，作为 GitHub 仓库首页默认展示
- `README.en.md`：英文入口与简要说明，供英文读者手动切换查看

这种做法的好处是简单、稳定，也最符合 GitHub 仓库常见做法。文档内部链接全部使用相对路径，上传到 GitHub 后可以直接工作。

## 第三方开源技术与许可证

本项目当前直接依赖的第三方库，已在本地 NuGet 元数据中核对其许可证信息。运行时相关依赖如下：

| 组件 | 用途 | 许可证 | 说明 |
| --- | --- | --- | --- |
| `LibreHardwareMonitorLib` `0.9.4` | 硬件传感器读取 | `MPL-2.0` | 若分发修改后的该库源码文件，需要继续保留对应文件的 MPL-2.0 条款 |
| `Microsoft.Diagnostics.Tracing.TraceEvent` `3.1.6` | ETW 事件采集与解析 | `MIT` | 需保留版权与许可证声明 |
| `System.Management` `9.0.0` | WMI 数据读取 | `MIT` | 需保留版权与许可证声明 |
| `Hardcodet.NotifyIcon.Wpf` `1.1.0` | WPF 系统托盘图标 | `CPOL-1.02` | 分发源码或二进制时应附带许可证文本或其 URI，并保留原始声明 |

开发 / 测试阶段还使用了以下依赖：

- `xunit` `2.5.3`，`Apache-2.0`
- `xunit.runner.visualstudio` `2.5.3`，`Apache-2.0`
- `coverlet.collector` `6.0.0`，`MIT`
- `Microsoft.NET.Test.Sdk` `17.8.0`，`MIT`

更详细的第三方组件说明见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 合规说明

- 第三方组件的许可证，只约束对应组件本身，不等于本项目整体许可证
- 当前仓库尚未单独声明 `GearGauge` 自身许可证；上传到 GitHub 并不等于自动开源
- 如果后续希望明确允许他人复制、修改、分发本项目代码，请在仓库根目录补充合适的 `LICENSE` 文件
- 如果你分发打包后的可执行文件，建议同时附带本仓库的 `README.md` 与 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)，以便保留必要的第三方许可证说明

## 仓库结构

- `GearGauge.UI`：WPF 桌面界面
- `GearGauge.Cli`：命令行采样工具
- `GearGauge.Hardware`：硬件、FPS、网络采集逻辑
- `GearGauge.Core`：模型与接口契约
- `GearGauge.Tests`：单元测试
- `assets`：图标等静态资源

## 致谢

感谢 `LibreHardwareMonitor`、`.NET` 运行时、`TraceEvent`、`xUnit` 等开源项目提供基础能力。本项目只是把这些能力按当前需求做了整合与取舍。
