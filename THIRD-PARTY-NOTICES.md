# Third-Party Notices

This file summarizes the third-party packages directly referenced by the current project files and the license information found in the local NuGet package metadata that was restored for this workspace.

It is not legal advice. If you redistribute source code or packaged binaries, review the original license texts linked below.

## Runtime Dependencies

### LibreHardwareMonitorLib 0.9.4

- Purpose: hardware sensor access
- License: `MPL-2.0`
- Project: <https://github.com/LibreHardwareMonitor/LibreHardwareMonitor>
- Notes: if you modify and redistribute files from this library, the modified files remain subject to MPL-2.0

### Microsoft.Diagnostics.Tracing.TraceEvent 3.1.6

- Purpose: ETW event collection and parsing
- License: `MIT`
- Project: <https://github.com/microsoft/perfview>
- Notes: keep the copyright and license notice

### System.Management 9.0.0

- Purpose: WMI access
- License: `MIT`
- Project: <https://github.com/dotnet/runtime>
- Notes: keep the copyright and license notice

### Hardcodet.NotifyIcon.Wpf 1.1.0

- Purpose: WPF tray icon integration
- License: `CPOL-1.02`
- Project: <https://github.com/hardcodet/wpf-notifyicon>
- Notes: when redistributing source or binaries that include this package, keep the license text or URI and preserve the original notices

## Development And Test Dependencies

### xunit 2.5.3

- Purpose: unit testing
- License: `Apache-2.0`
- Project: <https://xunit.net/>

### xunit.runner.visualstudio 2.5.3

- Purpose: Visual Studio test runner integration
- License: `Apache-2.0`
- Project: <https://xunit.net/>

### coverlet.collector 6.0.0

- Purpose: test coverage collection
- License: `MIT`
- Project: <https://github.com/coverlet-coverage/coverlet>

### Microsoft.NET.Test.Sdk 17.8.0

- Purpose: .NET test host and SDK integration
- License: `MIT`
- Project: <https://github.com/microsoft/vstest>

## Important Boundary

- Third-party package licenses do not define the license of `GearGauge` itself
- If this repository is intended to be open source, add a root `LICENSE` file for this project
- A practical redistribution bundle should include `README.md` and this `THIRD-PARTY-NOTICES.md`
