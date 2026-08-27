# WinNotch

**A contextual action surface for Windows.**

WinNotch sits at the top-center of the desktop and stays quiet while nothing needs attention. Relevant events can temporarily surface actions or information, then return to the compact idle surface.

Core interaction model:

`EVENT → UNDERSTAND CONTEXT → SHOW RELEVANT ACTION → RETURN`

## Current capabilities

| Capability | What it does |
|---|---|
| **File Shelf** | Hold dropped files/folders by path metadata, copy them as real Windows file-drop clipboard items, or drag them back out to another target. |
| **Smart Clipboard** | Event-driven clipboard monitoring for contextual content such as URLs and file paths. Plain text can remain silent depending on reaction level. |
| **Media Companion** | Optional SMTC integration for current media information and supported transport controls. Media is opt-in because WinRT media management adds resident memory cost. |
| **Screenshot Bridge** | Detect images produced through the Windows clipboard/Snipping Tool and surface a compact screenshot notification. |

Window pinning / arbitrary external-window `TOPMOST` management is intentionally not part of WinNotch. It does not fit the contextual-surface scope and can interfere with normal Windows z-order behavior.

## Behavior

- **Visibility:** Auto, Always Show, or Hidden.
- **Reaction level:** Quiet, Balanced, or Active.
- **Fullscreen:** primarily event-driven, with a low-frequency foreground-window reliability check in Auto mode.
- **Disabled capabilities:** their optional services are not kept alive.
- **Privacy-aware clipboard:** respects `CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR`.

## Performance

WinNotch is designed to do no continuous work while idle beyond the small fullscreen reliability check used in Auto visibility mode. Actual memory depends on the enabled integrations and the WPF/.NET runtime, so the project does not claim a fixed 15 MB process limit.

Use a direct Release build when measuring memory or CPU. The Settings diagnostics panel and Windows CI smoke test can be used for comparison.

## Tech stack

- .NET 8
- WPF
- Win32 P/Invoke
- DWM / native window-region handling
- Windows SMTC / WinRT for optional media integration

## Build and run

```powershell
dotnet restore WinNotch.sln
dotnet build WinNotch.sln -c Release
.\src\WinNotch.TrayApp\bin\Release\net8.0-windows10.0.19041.0\WinNotch.exe
```

For local development/retesting, the helper script gracefully closes a running WinNotch instance before rebuilding:

```powershell
.\scripts\rebuild-release.ps1
```

## Architecture

```text
WinNotch/
├── src/WinNotch.Common/   shared settings, constants and state logic
├── src/WinNotch.Core/     Win32 interop and optional services
├── src/WinNotch.UI/       notch surface, views and motion
├── src/WinNotch.TrayApp/  application lifecycle, tray and settings
└── tests/WinNotch.Tests/  regression tests
```

## Design principles

- Contextual rather than dashboard-like.
- Compact while idle.
- No arbitrary external-window z-order manipulation.
- Native hit-testing outside the actual notch surface.
- Optional integrations should have explicit lifecycle cleanup.
- Runtime claims should be measured rather than inferred from UI size.

## License

MIT
