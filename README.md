# WinNotch 🎯

**Windows Desktop Widget - Always-on-top, Borderless Notch**

A lightweight, always-on-top widget that sits at the top-center of your screen. Think of it as a software "notch" for your desktop — but smart, extensible, and resource-friendly.

## Features

| Module | Description | Status |
|--------|-------------|--------|
| **Module A** - Drag & Drop Path Extractor | Drop files/folders to see paths, open in Explorer, copy to clipboard | 🔨 In Progress |
| **Module B** - Clipboard Sniffer | Passive clipboard monitoring via Win32 API (no polling!) | 🔨 In Progress |
| **Module C** - Media Companion | SMTC integration - album art, controls, song info | 🔨 In Progress |
| **Module D** - Window Pinner | Drag title bars to pin windows always-on-top | 🔨 In Progress |
| **Module E** - Screenshot Bridge | Auto-detect Win+Shift+S screenshots | 🔨 In Progress |

## Performance Budget

- **RAM**: ≤ 15 MB (idle)
- **CPU**: ≤ 0.5% (idle), 0% (sleep state)
- **No polling** - entirely event-driven architecture

## Tech Stack

- .NET 8 (WPF) + Win32 API (P/Invoke)
- CommunityToolkit.Mvvm
- Native Win32 transparency (WS_EX_LAYERED + per-pixel alpha)
- DWM composited transparency (DwmExtendFrameIntoClientArea)
- Workstation GC + Concurrent mode

## Building

```bash
dotnet build WinNotch.sln
```

## Architecture

```
WinNotch/
├── WinNotch.Common/    → Shared constants and types
├── WinNotch.Core/      → Win32 interop, services, business logic
├── WinNotch.UI/        → WPF views, animations, converters
├── WinNotch.TrayApp/   → System tray, settings, module management
└── WinNotch.Tests/     → Unit tests
```

## Design Principles

- **Event-Driven**: No polling. All interactions use Win32 hooks and events.
- **Native Transparency**: No `AllowsTransparency="True"`. Uses `WS_EX_LAYERED` + `DwmExtendFrameIntoClientArea` for zero-overhead transparency.
- **Click-Through**: `WM_NCHITTEST` returns `HTTRANSPARENT` outside the notch area — your desktop remains fully interactive.
- **Module Isolation**: Each module can be independently enabled/disabled. Disabled modules = 0 resource consumption.
- **Privacy-Aware**: Clipboard listener respects `CF_EXCLUDECLIPBOARDCONTENTFROMMONITOR` for password managers.

## License

MIT
