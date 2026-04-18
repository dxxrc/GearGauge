# GearGauge

English | [简体中文](README.md)

> Note
>
> This project was largely planned, implemented, and documented with AI assistance. I am not a professional developer in this area; if anything in the code, docs, or license notes is inaccurate, corrections through issues or pull requests are welcome.

## Overview

GearGauge is a Windows 10 / Windows 11 real-time monitoring tool for CPU, GPU, memory, FPS, and network activity.

It currently ships with:

- `GearGauge.UI`: a WPF desktop app with tray integration, overlay, and taskbar widget
- `GearGauge.Cli`: a CLI sampler that outputs JSON snapshots

The Chinese [README.md](README.md) is the primary document. This English file exists because GitHub shows only one repository README by default, so the repository uses Chinese as the landing page and keeps this file as a manual English entry point.

## What It Does

- Monitors CPU / GPU usage, temperature, power, and clocks
- Shows memory usage and active network throughput
- Estimates display FPS and game FPS
- Supports tray controls, overlay window, and taskbar widget
- Provides machine-readable CLI output for scripting

## How It Works

- Hardware metrics are primarily collected through `LibreHardwareMonitorLib`
- CPU metadata and some fallback values come from `WMI`
- CPU clock data may be supplemented by `PDH`
- FPS detection combines `DXGI`, `ETW`, and `DWM`-based strategies
- Network speed is computed from byte deltas between snapshots

## Build

```powershell
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_HOME="$PWD\.dotnet"
dotnet restore GearGauge.sln
dotnet build GearGauge.sln -c Release --no-restore -m:1
dotnet test GearGauge.Tests\GearGauge.Tests.csproj -c Release --no-build -m:1
```

Run the UI:

```powershell
dotnet run --project GearGauge.UI\GearGauge.UI.csproj -c Release --no-build
```

Run the CLI:

```powershell
dotnet run --project GearGauge.Cli\GearGauge.Cli.csproj -c Release --no-build -- --pretty
```

## Third-Party Licenses

Runtime-facing direct dependencies currently include:

- `LibreHardwareMonitorLib` `0.9.4` under `MPL-2.0`
- `Microsoft.Diagnostics.Tracing.TraceEvent` `3.1.6` under `MIT`
- `System.Management` `9.0.0` under `MIT`
- `Hardcodet.NotifyIcon.Wpf` `1.1.0` under `CPOL-1.02`

Detailed notices are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## License Status

This repository does not currently declare a license for `GearGauge` itself. Uploading code to GitHub does not automatically make it open source. Third-party licenses apply only to their respective components.
